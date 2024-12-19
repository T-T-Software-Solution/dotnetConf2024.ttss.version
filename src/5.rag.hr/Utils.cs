using System.Text;

public static class Utils
{
    public static void ReadDataFromConfig(
        out bool useAzureOpenAI,
        out string azureChatModel, out string azureTextEmbeddingModel, out string azureAPIEndpoint, out string azureAPIKey,
        out string ollamaChatModel, out string ollamaTextEmbeddingModel, out string ollamaAPIEndpoint)
    
    {
        IConfiguration configuration = new ConfigurationBuilder()
                        .AddJsonFile("appsettings.json")
                        .Build();

        useAzureOpenAI = bool.Parse(configuration["useAzureOpenAI"] ?? throw new ArgumentNullException("useAzureOpenAI"));

        azureChatModel = configuration["AzureOpenAI:ChatModel"] ?? throw new ArgumentNullException("AzureOpenAI:ChatModel");
        azureTextEmbeddingModel = configuration["AzureOpenAI:TextEmbeddingModel"] ?? throw new ArgumentNullException("AzureOpenAI:TextEmbeddingModel");
        azureAPIEndpoint = configuration["AzureOpenAI:Endpoint"] ?? throw new ArgumentNullException("AzureOpenAI:Endpoint");
        azureAPIKey = configuration["AzureOpenAI:ApiKey"] ?? throw new ArgumentNullException("AzureOpenAI:ApiKey");

        ollamaChatModel = configuration["Ollama:ChatModel"] ?? throw new ArgumentNullException("Ollama:ChatModel");
        ollamaTextEmbeddingModel = configuration["Ollama:TextEmbeddingModel"] ?? throw new ArgumentNullException("Ollama:TextEmbeddingModel");
        ollamaAPIEndpoint = configuration["Ollama:Endpoint"] ?? throw new ArgumentNullException("Ollama:Endpoint");

    }
    public static AzureOpenAIClient CreateAzureOpenAIClient(string endpoint, string apiKey)
    {
        return new AzureOpenAIClient(new Uri(endpoint), new ApiKeyCredential(apiKey));
    }
    public static IEnumerable<Employee> LoadEmployee(string path, int limit = 10)
    {
        var employeeData = File.ReadAllText(path);
        var employees = JsonSerializer.Deserialize<List<Employee>>(employeeData);
        return employees.Take(limit);
    }

    public static IEnumerable<ManualChunk> LoadManualChunks(string path)
    {
        var chunkData = File.ReadAllText(path);
        var chunks = JsonSerializer.Deserialize<List<ManualChunk>>(chunkData);
        return chunks;
    }

    public static async void LoadManualsIntoVectorStore(string path, ProductManualService productManualService)
    {
        var manuals = LoadManualChunks(path);
        await productManualService.InsertManualChunksAsync(manuals);
    }

    // แปลงสถานะการทำงานเป็นไทย
    private static string TranslateEmploymentStatus(EmploymentStatus status)
    {
        return status switch
        {
            EmploymentStatus.Open => "ทำงานอยู่",
            EmploymentStatus.Closed => "ไม่ได้ทำแล้ว",
            _ => "ไม่ทราบสถานะ"
        };
    }

    private static void SetupEmployeeInfo(IEnumerable<Employee> employees, out Employee? employee, out string messageText)
    {
        // User selects ticket
        employee = AnsiConsole.Prompt(
                new SelectionPrompt<Employee>()
                    .Title("Select employee")
                    .PageSize(10)
                    .MoreChoicesText("[grey](Move up and down to reveal more choices)[/]")
                    .AddChoices(employees)
                    .UseConverter(employee => $"รหัสพนักงาน: {employee.EmployeeId.ToString()} - ({TranslateEmploymentStatus(employee.EmploymentStatus)}) {employee.FullName} ({employee.Position})")
            );
        if (employee == null)
        {
            Console.WriteLine("Employ not found.");
        }

        messageText = "";
        StringBuilder messageBuilder = new StringBuilder();
        // Employee formatted for display
        foreach (var interaction in employee.InteractionHistory)
        {
            messageBuilder.AppendLine($"[grey]วันที่: {interaction.Date}[/]");
            messageBuilder.AppendLine($"[blue]พนักงานสอบถามเรื่อง: {interaction.Content}[/]");
            messageBuilder.AppendLine($"[green]AI ตอบว่า: {interaction.AIResponse}[/]");
            messageBuilder.AppendLine();
        }

        messageText = messageBuilder.ToString();
    }
    
    private static void SetupPanelAndDisplay(Employee? employee, Panel panel)
    {
        panel.Header = new PanelHeader($"Customer Messages for Employee ID: {employee.EmployeeId}");
        AnsiConsole.Write(panel);
    }

    private static async Task<ChatCompletion> GenerateSummary(EmployeeSummarizer summaryGenerator, string messageText)
    {
        return await summaryGenerator.GenerateLongSummaryAsync(messageText);
    }

    public static async Task InspectTicketWithSemanticSearchAsync(IEnumerable<Employee> employees, 
        EmployeeSummarizer summaryGenerator, ProductManualService productManualService, IChatClient chatClient)
    {
        Employee? employee;
        string messageText;
        SetupEmployeeInfo(employees, out employee, out messageText);

        ChatCompletion summary = await GenerateSummary(summaryGenerator, messageText);

        var panel = new Panel($"[olive]{messageText}AI สรุปการสนทนาว่า: {summary}[/]");
        
        SetupPanelAndDisplay(employee, panel);

        // Chat loop
        var prompt = AnsiConsole.Prompt(
            new SelectionPrompt<string>()
                .Title("กรุณาสอบถามรายละเอียดของพนักงานเพิ่มเติม เช่น วันหยุด, สิทธิพิเศษและสวัสดิการ, ประวัติการทำงาน, ความสามารถ, ประวัติการขึ้นเงินเดือน, ประวัติการอบรม, ประวัติการเลื่อนตำแหน่ง, เป้าหมายในอาชีพ, ใบรับรอง, ความสนใจส่วนตัว")
                .PageSize(10)
                .MoreChoicesText("[grey](Move up and down to reveal more choices)[/]")
                .AddChoices(new[] { "Chat", "Back" })
        );

        if (prompt == "Back") return;

        if (prompt == "Chat")
        {
            while (true)
            {
                var query =
                    AnsiConsole
                        .Prompt(
                            new TextPrompt<string>("ระบุสิ่งที่ต้องการถาม (type 'quit' to exit)")
                                .PromptStyle("green")
                        );

                if (query == "quit") break;

                // RAG loop
                // [1] Search for relevant documents
                var manualChunks = await productManualService.GetManualChunksAsync(query, employee.EmployeeId);

                // [2] Augment prompt with search results
                var context = (await manualChunks.Results.ToListAsync()).Select(r => $"- {r.Record.Text}");
                var contextString = string.Join("\n", context);

                var message = $"""

                Using the following data sources as context

                ## Context
                {contextString}

                ## Instruction

                Answer the user query: {query}

                ให้ตอบเป็นภาษาไทยทั้งหมดเท่าที่เป็นไปได้
                Response: 
                
                """;    

                // [3] Generate response
                var response = await chatClient.CompleteAsync(message);
                
                // [3.1] Display User Prompt
                AnsiConsole.MarkupLine($"\n[bold blue]สิ่งที่ต้องการสืบค้นเพิ่มเติม[/]");
                AnsiConsole.MarkupLine("[bold blue]---------------[/]");
                AnsiConsole.MarkupLine($"[blue]{query}[/]");

                // [3.2] Display Result
                AnsiConsole.MarkupLine($"\n[bold green]ผลลัพธ์ที่ได้[/]");
                AnsiConsole.MarkupLine("[bold green]---------------[/]");
                AnsiConsole.MarkupLine($"[green]{response}[/]");

                // [3.3] Display PDF Content from Vector Search
                AnsiConsole.MarkupLine($"\n[bold yellow]ข้อมูลที่ระบบหาเจอจาก PDF Files[/]");
                AnsiConsole.MarkupLine("[bold yellow]---------------[/]");
                AnsiConsole.MarkupLine($"[yellow]Employee Id: {employee.EmployeeId}[/]");
                AnsiConsole.MarkupLine($"[yellow]\nเนื้อหาจาก PDF ที่เกี่ยวข้องจากการค้นหาด้วย Vector Search[/]");
                AnsiConsole.MarkupLine($"[yellow]{contextString}[/]");
            }
        }
    }
}
