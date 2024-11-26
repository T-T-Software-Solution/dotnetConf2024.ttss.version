#pragma warning disable
using Microsoft.SemanticKernel.Connectors.InMemory;

string azureChatModel, azureTextEmbeddingModel, azureAPIEndpoint, azureAPIKey;
string ollamaChatModel, ollamaTextEmbeddingModel, ollamaAPIEndpoint;

Utils.ReadDataFromConfig(
    out azureChatModel, out azureTextEmbeddingModel, out azureAPIEndpoint, out azureAPIKey,
    out ollamaChatModel, out ollamaTextEmbeddingModel, out ollamaAPIEndpoint);

var useAzureOpenAI = true; // Use OpenAI chat completion models

IChatClient chatClient;
IEmbeddingGenerator<string, Embedding<float>> embeddingGenerator;

if(useAzureOpenAI)
{
    chatClient = Utils.CreateAzureOpenAIClient(azureAPIEndpoint, azureAPIKey)
        .AsChatClient(azureChatModel);
        
    embeddingGenerator = Utils.CreateAzureOpenAIClient(azureAPIEndpoint, azureAPIKey)
        .AsEmbeddingGenerator(azureTextEmbeddingModel);
}
else
{
    chatClient = new OllamaApiClient(new Uri(ollamaAPIEndpoint), ollamaChatModel);
    embeddingGenerator = new OllamaApiClient(new Uri(ollamaAPIEndpoint), ollamaTextEmbeddingModel);
}

// Configure product manual service
var vectorStore = new InMemoryVectorStore();
var productManualService = new ProductManualService(embeddingGenerator, vectorStore, useAzureOpenAI);
// Ingest manuals

if (!File.Exists("./data/manual-chunks.json"))
{
    var manualIngestor = new ManualIngestor(embeddingGenerator);
    await manualIngestor.RunAsync("./data/manuals", "./data");
}

// Load tickets and manuals
var tickets = LoadTickets("./data/tickets.json");
LoadManualsIntoVectorStore("./data/manual-chunks.json", productManualService);

// Service configurations
var summaryGenerator = new TicketSummarizer(chatClient);

while (true)
{
    var prompt =
        AnsiConsole
            .Prompt(
                new SelectionPrompt<string>()
                    .Title("Enter a command")
                    .PageSize(10)
                    .MoreChoicesText("[grey](Move up and down to reveal more choices)[/]")
                    .AddChoices(new[] { "Inspect ticket", "Quit" })
            );

    if (prompt == "Quit") break;

    if (prompt == "Inspect ticket")
    {
        // No AI
        //InspectTicket(tickets);

        // With AI Summaries
        // await InspectTicketWithAISummaryAsync(tickets, summaryGenerator);

        // With Semantic Search 
        await InspectTicketWithSemanticSearchAsync(tickets, summaryGenerator, productManualService, chatClient);
    }
}
