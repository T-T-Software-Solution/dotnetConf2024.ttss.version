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
    public static IEnumerable<Ticket> LoadTickets(string path, int limit = 10)
    {
        var ticketData = File.ReadAllText(path);
        var tickets = JsonSerializer.Deserialize<List<Ticket>>(ticketData);
        return tickets.Take(limit);
    }

    public static void SaveTickets(string path, IEnumerable<Ticket> tickets)
    {
        var ticketData = JsonSerializer.Serialize(tickets);
        File.WriteAllText(path, ticketData);
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

    private static void SetupTicketInfo(IEnumerable<Ticket> tickets, out Ticket? ticket, out string messageText)
    {
        // User selects ticket
        ticket = AnsiConsole.Prompt(
                new SelectionPrompt<Ticket>()
                    .Title("Select ticket")
                    .PageSize(10)
                    .MoreChoicesText("[grey](Move up and down to reveal more choices)[/]")
                    .AddChoices(tickets)
                    .UseConverter(ticket => $"{ticket.TicketId.ToString()} - ({ticket.TicketStatus}) {ticket.ShortSummary}")
            );
        if (ticket == null)
        {
            Console.WriteLine("Ticket not found.");
        }

        // Tickets formatted for display
        var formattedMessages = ticket.Messages.Select(m =>
            m.IsCustomerMessage ? $"\n[blue]ลูกค้า: {m.Text}[/]" : $"\n[green]เจ้าหน้าที่: {m.Text}[/]");
        messageText = string.Join("\n", formattedMessages);
    }
    
    private static void SetupPanelAndDisplay(Ticket? ticket, Panel panel)
    {
        panel.Header = new PanelHeader($"Customer Messages for Ticket ID: {ticket.TicketId}");
        AnsiConsole.Write(panel);
    }

    private static async Task<ChatCompletion> GenerateSummary(TicketSummarizer summaryGenerator, string messageText)
    {
        return await summaryGenerator.GenerateLongSummaryAsync(messageText);
    }

    public static async Task InspectTicketWithSemanticSearchAsync(IEnumerable<Ticket> tickets, TicketSummarizer summaryGenerator, ProductManualService productManualService, IChatClient chatClient)
    {
        Ticket? ticket;
        string messageText;
        SetupTicketInfo(tickets, out ticket, out messageText);

        ChatCompletion summary = await GenerateSummary(summaryGenerator, messageText);

        var panel = new Panel($"[olive]{messageText}\n\nAI สรุปให้ว่า: {summary}[/]");
        
        SetupPanelAndDisplay(ticket, panel);

        // Chat loop
        var prompt = AnsiConsole.Prompt(
            new SelectionPrompt<string>()
                .Title("Enter a command")
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
                            new TextPrompt<string>("Enter a message (type 'quit' to exit)")
                                .PromptStyle("green")
                        );

                if (query == "quit") break;

                // RAG loop
                // [1] Search for relevant documents
                var manualChunks = await productManualService.GetManualChunksAsync(query, ticket.ProductId.Value);

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
                AnsiConsole.MarkupLine($"[yellow]Product Id: {ticket.ProductId.Value}[/]");
                AnsiConsole.MarkupLine($"[yellow]\nเนื้อหาจาก PDF ที่เกี่ยวข้องจากการค้นหาด้วย Vector Search[/]");
                AnsiConsole.MarkupLine($"[yellow]{contextString}[/]");

                // [3.4] Display RAG Prompt
                AnsiConsole.MarkupLine($"\n[bold Purple]Prompt ที่ส่งไปหา Chat Completion Model[/]");
                AnsiConsole.MarkupLine("[bold Purple]---------------[/]");
                AnsiConsole.MarkupLine($"[Purple]{message}[/]");
            }
        }
    }
}
