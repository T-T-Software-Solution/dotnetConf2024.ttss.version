public static class Utils
{
    public static void ReadDataFromConfig(
        out string azureChatModel, out string azureTextEmbeddingModel, out string azureAPIEndpoint, out string azureAPIKey,
        out string ollamaChatModel, out string ollamaTextEmbeddingModel, out string ollamaAPIEndpoint)

    {
        IConfiguration configuration = new ConfigurationBuilder()
                        .AddJsonFile("appsettings.json")
                        .Build();

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

    public static async Task RAGAsync(IEnumerable<Ticket> tickets, TicketSummarizer summaryGenerator, ProductManualService productManualService, IChatClient chatClient)
    {
        var prompt = "URL";
        var ticketId = 104;
        // RAG loop
        // [1] Search for relevant documents
        var manualChunks = await productManualService.GetManualChunksAsync(prompt, ticketId);

        // [2] Augment prompt with search results
        var context = (await manualChunks.Results.ToListAsync()).Select(r => $"- {r.Record.Text}");
        var contextString = string.Join("\n", context);

        var message = $"""

        Using the following data sources as context

        ## Context
        {contextString}

        ## Instruction

        Answer the user query: {prompt}

        ให้ตอบเป็นภาษาไทยทั้งหมดเท่าที่เป็นไปได้
        Response: 
        
        """;

        // [3] Generate response
        var response = await chatClient.CompleteAsync(message);

        // [3.1] Display User Prompt
        AnsiConsole.MarkupLine($"\n[bold blue]สิ่งที่ต้องการสืบค้นเพิ่มเติม[/]");
        AnsiConsole.MarkupLine("[bold blue]---------------[/]");
        AnsiConsole.MarkupLine($"[blue]{prompt}[/]");

        // [3.2] Display Result
        AnsiConsole.MarkupLine($"\n[bold green]ผลลัพธ์ที่ได้[/]");
        AnsiConsole.MarkupLine("[bold green]---------------[/]");
        AnsiConsole.MarkupLine($"[green]{response}[/]");

        // [3.3] Display PDF Content from Vector Search
        AnsiConsole.MarkupLine($"\n[bold yellow]ข้อมูลที่ระบบหาเจอจาก PDF Files[/]");
        AnsiConsole.MarkupLine("[bold yellow]---------------[/]");
        AnsiConsole.MarkupLine($"[yellow]Product Id: {ticketId}[/]");
        AnsiConsole.MarkupLine($"[yellow]\nเนื้อหาจาก PDF ที่เกี่ยวข้องจากการค้นหาด้วย Vector Search[/]");
        AnsiConsole.MarkupLine($"[yellow]{contextString}[/]");
    }
}
