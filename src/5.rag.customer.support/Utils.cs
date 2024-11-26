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
                    .UseConverter(ticket => $"{ticket.TicketId.ToString()} - {ticket.ShortSummary}")
            );
        if (ticket == null)
        {
            Console.WriteLine("Ticket not found.");
        }

        // Tickets formatted for display
        var formattedMessages = ticket.Messages.Select(m =>
            m.IsCustomerMessage ? $"[blue]Customer: {m.Text}[/]" : $"[green]Agent: {m.Text}[/]");
        messageText = string.Join("\n", formattedMessages);
    }
    private static void SetupPanelAndDisplay(Ticket? ticket, Panel panel)
    {
        panel.Header = new PanelHeader($"Customer Messages for Ticket ID: {ticket.TicketId}");
        AnsiConsole.Write(panel);
    }

    public static void InspectTicket(IEnumerable<Ticket> tickets)
    {
        Ticket? ticket;
        string messageText;
        SetupTicketInfo(tickets, out ticket, out messageText);

        // Display tickets
        var panel = new Panel(messageText);

        SetupPanelAndDisplay(ticket, panel);
    }

    public static async Task InspectTicketWithAISummaryAsync(IEnumerable<Ticket> tickets, TicketSummarizer summaryGenerator)
    {
        Ticket? ticket;
        string messageText;
        SetupTicketInfo(tickets, out ticket, out messageText);

        ChatCompletion summary = await GenerateSummary(summaryGenerator, messageText);

        // Display tickets
        var panel = new Panel($"[olive]Summary: {summary}[/]\n\n{messageText}");

        SetupPanelAndDisplay(ticket, panel);
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

        var panel = new Panel($"[olive]Summary: {summary}[/]\n\n{messageText}");
        
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
                var productIdInfo = (await manualChunks.Results.ToListAsync()).FirstOrDefault();
                var productId = string.Empty;
                if(productIdInfo != null)
                {
                    productId = productIdInfo.Record.ProductId.ToString();
                }
                
                var context = (await manualChunks.Results.ToListAsync()).Select(r => $"- {r.Record.Text}");

                var message = $"""
                Using the following data sources as context

                ## Product Id
                {string.Join("\n", productId.Distinct())}
                
                ## Context
                {string.Join("\n", context)}

                ## Instruction

                Answer the user query: {query}

                Response: 
                """;    

                AnsiConsole.MarkupLine($"[bold yellow]{message}[/]");
                AnsiConsole.MarkupLine("[bold yellow]---------------[/]");

                // [3] Generate response
                var response = await chatClient.CompleteAsync(message);
                Console.WriteLine(response);

                //AnsiConsole.MarkupLine($"[bold yellow]{response}[/]");
            }
        }
    }
}
