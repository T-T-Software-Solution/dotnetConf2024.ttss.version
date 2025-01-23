#pragma warning disable
using Microsoft.SemanticKernel.Connectors.Qdrant;
using Microsoft.Extensions.VectorData;
using Qdrant.Client;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.AI;
using Microsoft.SemanticKernel.Memory;
using Microsoft.SemanticKernel.Embeddings;
using Microsoft.SemanticKernel.Connectors.AzureOpenAI;
using Microsoft.SemanticKernel.Connectors.Ollama;
using OllamaSharp;
using System.Text.Json;

var hotels = await GetHotels();

bool useQdrantCloud;
string qdrantHost, qdrantApiKey;

bool useAzureOpenAI;
string azureTextEmbeddingModel, azureAPIEndpoint, azureAPIKey;
string ollamaTextEmbeddingModel, ollamaAPIEndpoint;

ReadDataFromConfig(
    out useQdrantCloud,
    out qdrantHost, out qdrantApiKey,
    out useAzureOpenAI,
    out azureTextEmbeddingModel, out azureAPIEndpoint, out azureAPIKey,
    out ollamaTextEmbeddingModel, out ollamaAPIEndpoint);

var embeddingGenerator = GetEmbeddingGenerator(useAzureOpenAI, azureTextEmbeddingModel, azureAPIEndpoint, azureAPIKey, ollamaTextEmbeddingModel, ollamaAPIEndpoint);

QdrantVectorStore vectorStore = CreateQdrantVectorStoreClient(useQdrantCloud, qdrantHost, qdrantApiKey);
var collection = await CreateQdrantCollection(vectorStore);
await UpsertPoints(collection, embeddingGenerator, hotels);

await TrytoGetAndDisplayaRecordWithoutVectorSearch(collection: collection, hotelId: 1);

do
{
    string? userPrompt = WaitForUserPrompt();
    await PerformVectorSearch(collection, userPrompt, embeddingGenerator);
} while (true);

static void ReadDataFromConfig(
    out bool useQdrantCloud, out string qdrantHost, out string qdrantApiKey,
    out bool useAzureOpenAI,
    out string azureTextEmbeddingModel, out string azureAPIEndpoint, out string azureAPIKey,
    out string ollamaTextEmbeddingModel, out string ollamaAPIEndpoint)

{
    IConfiguration configuration = new ConfigurationBuilder()
                    .AddJsonFile("appsettings.json")
                    .Build();

    useQdrantCloud = bool.Parse(configuration["useQdrantCloud"] ?? throw new ArgumentNullException("useQdrantCloud"));
    qdrantHost = configuration["QdrantCloud:Host"] ?? throw new ArgumentNullException("QdrantCloud:Host");
    qdrantApiKey = configuration["QdrantCloud:ApiKey"] ?? throw new ArgumentNullException("QdrantCloud:ApiKey");

    useAzureOpenAI = bool.Parse(configuration["useAzureOpenAI"] ?? throw new ArgumentNullException("useAzureOpenAI"));
    azureTextEmbeddingModel = configuration["AzureOpenAI:TextEmbeddingModel"] ?? throw new ArgumentNullException("AzureOpenAI:TextEmbeddingModel");
    azureAPIEndpoint = configuration["AzureOpenAI:Endpoint"] ?? throw new ArgumentNullException("AzureOpenAI:Endpoint");
    azureAPIKey = configuration["AzureOpenAI:ApiKey"] ?? throw new ArgumentNullException("AzureOpenAI:ApiKey");

    ollamaTextEmbeddingModel = configuration["Ollama:TextEmbeddingModel"] ?? throw new ArgumentNullException("Ollama:TextEmbeddingModel");
    ollamaAPIEndpoint = configuration["Ollama:Endpoint"] ?? throw new ArgumentNullException("Ollama:Endpoint");
}

static ITextEmbeddingGenerationService GetEmbeddingGenerator(
    bool useAzureOpenAI,
    string azureTextEmbeddingModel, string azureAPIEndpoint, string azureAPIKey,
    string ollamaTextEmbeddingModel, string ollamaAPIEndpoint)
{
    var ollamaClient = new OllamaApiClient(
        uriString: ollamaAPIEndpoint,    // E.g. "http://localhost:11434" if Ollama has been started in docker as described above.
        defaultModel: ollamaTextEmbeddingModel // E.g. "mxbai-embed-large" if mxbai-embed-large was downloaded as described above.
    );
    ITextEmbeddingGenerationService embeddingGenerator = ollamaClient.AsTextEmbeddingGenerationService();

    if (useAzureOpenAI)
    {
        embeddingGenerator = new AzureOpenAITextEmbeddingGenerationService(
            deploymentName: azureTextEmbeddingModel,
            endpoint: azureAPIEndpoint,
            apiKey: azureAPIKey);
    }

    return embeddingGenerator;
}

static async Task<List<Hotel>> GetHotels()
{
    string jsonFilePath = "hotels.json";
    string jsonString = await File.ReadAllTextAsync(jsonFilePath);
    return JsonSerializer.Deserialize<List<Hotel>>(jsonString);
}

static async Task<ReadOnlyMemory<float>> GenerateEmbeddingAsync(string textToVectorize, ITextEmbeddingGenerationService embeddingGenerator)
{
    return await embeddingGenerator.GenerateEmbeddingAsync(textToVectorize);
}

static QdrantVectorStore CreateQdrantVectorStoreClient(bool useQdrantCloud, string qdrantHost, string qdrantApiKey)
{
    var qdrantClient = new QdrantClient(host: "localhost", port: 6334);
    if (useQdrantCloud)
        qdrantClient = new QdrantClient(host: qdrantHost, https: true, apiKey: qdrantApiKey);

    return new QdrantVectorStore(qdrantClient);
}

static async Task<IVectorStoreRecordCollection<ulong, HotelVectorStore>> CreateQdrantCollection(QdrantVectorStore vectorStore)
{
    var hotelDefinition = new VectorStoreRecordDefinition
    {
        Properties = new List<VectorStoreRecordProperty>
        {
            new VectorStoreRecordKeyProperty("HotelId", typeof(ulong)),
            new VectorStoreRecordDataProperty("HotelName", typeof(string)) { IsFilterable = true },
            new VectorStoreRecordDataProperty("Description", typeof(string)) { IsFullTextSearchable = true },
            new VectorStoreRecordVectorProperty("DescriptionEmbedding", typeof(float)) { Dimensions = 4, DistanceFunction = DistanceFunction.CosineDistance, IndexKind = IndexKind.Hnsw },
        }
    };


    var collection = vectorStore.GetCollection<ulong, HotelVectorStore>("skhotels");

    // Create the collection if it doesn't exist yet.
    await collection.CreateCollectionIfNotExistsAsync();

    return collection;
}

static async Task UpsertPoints(IVectorStoreRecordCollection<ulong, HotelVectorStore> collection, ITextEmbeddingGenerationService embeddingGenerator , List<Hotel> hotels)
{
    // Upsert all hotels to the collection
    foreach (var hotel in hotels)
    {
        await collection.UpsertAsync(new HotelVectorStore
        {
            HotelId = hotel.HotelId,
            HotelName = hotel.HotelName,
            Description = hotel.Description,
            DescriptionEmbedding = await GenerateEmbeddingAsync(hotel.Description, embeddingGenerator),
            Tags = hotel.Tags.ToArray()
        });
    }
}

static async Task TrytoGetAndDisplayaRecordWithoutVectorSearch(IVectorStoreRecordCollection<ulong, HotelVectorStore> collection, ulong hotelId)
{
    // Retrieve the upserted record.
    var hotel = await collection.GetAsync(hotelId);

    //Display the hotel info
    Console.WriteLine();
    Console.WriteLine(" Try to get and display a record without vector search: hotel id: " + hotelId);
    Console.WriteLine("---------------------------------------------");

    if (hotel == null)
    {
        Console.WriteLine("No hotel found with id: " + hotelId);
        return;
    }

    Console.WriteLine("Found a hotel: ");
    Console.WriteLine("- Id: " + hotel.HotelId);
    Console.WriteLine("- Name: " + hotel.HotelName);
    Console.WriteLine("- Tags: " + string.Join(", ", hotel.Tags));
    Console.WriteLine("- Description: " + hotel.Description);
    Console.WriteLine();
}

static async Task PerformVectorSearch(IVectorStoreRecordCollection<ulong, HotelVectorStore> collection, string? userPrompt, ITextEmbeddingGenerationService embeddingGenerator)
{
    // Generate a vector for your search text, using your chosen embedding generation implementation.
    ReadOnlyMemory<float> searchVector = await GenerateEmbeddingAsync(userPrompt, embeddingGenerator);

    // Do the search.
    var searchResult = await collection.VectorizedSearchAsync(searchVector, new() { Top = 5, IncludeTotalCount = true });

    // Inspect the returned HotelInVectorStore.
    await foreach (var record in searchResult.Results)
    {
        Console.WriteLine("Found hotel Id: " + record.Record.HotelId);
        Console.WriteLine("Found hotel name: " + record.Record.HotelName);
        Console.WriteLine("Found hotel tags: " + string.Join(", ", record.Record.Tags));
        Console.WriteLine("Found hotel description: " + record.Record.Description);
        Console.WriteLine("Found record score: " + record.Score);
        Console.WriteLine("");
    }
}

static string? WaitForUserPrompt()
{
    // Wait for user prompt to search for hotels.
    Console.WriteLine("กรอกข้อความที่ต้องการค้นหาเกี่ยวกับโรงแรม เช่น ราคาประหยัด, อยู่ในเมือง, หรูหรา, สระว่ายน้ำ: ");
    var userPrompt = Console.ReadLine();
    return userPrompt;
}

public class Hotel
{
    public ulong HotelId { get; set; }
    public string HotelName { get; set; }
    public string Description { get; set; }
    public List<string> Tags { get; set; }
}
public class HotelVectorStore
{
    [VectorStoreRecordKey]
    public ulong HotelId { get; set; }

    [VectorStoreRecordData(IsFilterable = true)]
    public string HotelName { get; set; }

    [VectorStoreRecordData(IsFullTextSearchable = true)]
    public string Description { get; set; }

    [VectorStoreRecordVector(Dimensions: EmbeddingDimensions.OllamaBEGm3EmbeddingSize, DistanceFunction.CosineSimilarity, IndexKind.Hnsw)]
    public ReadOnlyMemory<float>? DescriptionEmbedding { get; set; }

    [VectorStoreRecordData(IsFilterable = true)]
    public string[] Tags { get; set; }
}

internal class EmbeddingDimensions
{
    public const int OllamaBEGm3EmbeddingSize = 1024;
    public const int OpenAIEmbeddingSize = 1536;
}