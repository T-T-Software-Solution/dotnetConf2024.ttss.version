#pragma warning disable
using Microsoft.SemanticKernel.Connectors.InMemory;

// Configurations for Azure OpenAI and Ollama
bool useAzureOpenAI;
string azureChatModel, azureTextEmbeddingModel, azureAPIEndpoint, azureAPIKey;
string ollamaChatModel, ollamaTextEmbeddingModel, ollamaAPIEndpoint;

Utils.ReadDataFromConfig(
    out useAzureOpenAI,
    out azureChatModel, out azureTextEmbeddingModel, out azureAPIEndpoint, out azureAPIKey,
    out ollamaChatModel, out ollamaTextEmbeddingModel, out ollamaAPIEndpoint);

IChatClient chatClient;
IEmbeddingGenerator<string, Embedding<float>> embeddingGenerator;

// Create chat client and embedding generator 
// by selecting the appropriate implementation - Azure OpenAI or Ollama
if(useAzureOpenAI)
{
    var azureAI = Utils.CreateAzureOpenAIClient(azureAPIEndpoint, azureAPIKey);
    
    chatClient = azureAI.AsChatClient(azureChatModel);
    embeddingGenerator = azureAI.AsEmbeddingGenerator(azureTextEmbeddingModel);
}
else
{
    chatClient = new OllamaApiClient(new Uri(ollamaAPIEndpoint), ollamaChatModel);
    embeddingGenerator = new OllamaApiClient(new Uri(ollamaAPIEndpoint), ollamaTextEmbeddingModel);
}

// Ingest manuals manually if not already done
if (!File.Exists("./data/manual-chunks.json"))
{
    var manualIngestor = new ManualIngestor(embeddingGenerator);
    await manualIngestor.RunAsync("./data/manuals", "./data");
}

// Configure product manual service
var vectorStore = new InMemoryVectorStore();
var productManualService = new ProductManualService(embeddingGenerator, vectorStore, useAzureOpenAI);

// Load tickets
var tickets = Utils.LoadTickets("./data/tickets.json");

// Load manuals
Utils.LoadManualsIntoVectorStore("./data/manual-chunks.json", productManualService);

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
        // With Semantic Search 
        await Utils.InspectTicketWithSemanticSearchAsync(tickets, summaryGenerator, productManualService, chatClient);
    }
}
