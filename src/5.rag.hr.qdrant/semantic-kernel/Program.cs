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

using var ollamaClient = new OllamaApiClient(
    uriString: ollamaAPIEndpoint,    // E.g. "http://localhost:11434" if Ollama has been started in docker as described above.
    defaultModel: ollamaTextEmbeddingModel // E.g. "mxbai-embed-large" if mxbai-embed-large was downloaded as described above.
);

ITextEmbeddingGenerationService embeddingGenerator = ollamaClient.AsTextEmbeddingGenerationService();   

if(useAzureOpenAI)
{
    embeddingGenerator = new AzureOpenAITextEmbeddingGenerationService(
        deploymentName: azureTextEmbeddingModel,
        endpoint: azureAPIEndpoint,
        apiKey: azureAPIKey);
}

//var hotels = await GetHotels(embeddingGenerator);

QdrantVectorStore vectorStore = CreateQdrantVectorStoreClient(useQdrantCloud, qdrantHost, qdrantApiKey);
var collection = await CreateQdrantCollection(vectorStore);
//await UpsertPoints(collection, hotels);

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


static async Task<List<Hotel>> GetHotels(ITextEmbeddingGenerationService embeddingGenerator)
{
    ulong hotelId = 1;
    string descriptionText;
    var hotels = new List<Hotel>();

    // Hotel 1-5
    descriptionText = "โรงแรมที่มีความสุขสำหรับทุกคน";
    hotels.Add(new Hotel
    {
        HotelId = hotelId++,
        HotelName = "โรงแรมแห่งความสุข",
        Description = descriptionText,
        DescriptionEmbedding = await GenerateEmbeddingAsync(descriptionText, embeddingGenerator),
        Tags = new[] { "หรูหรา", "สระว่ายน้ำ" }
    });

    descriptionText = "สถานที่พักผ่อนสบาย ๆ ใกล้ชายหาด";
    hotels.Add(new Hotel
    {
        HotelId = hotelId++,
        HotelName = "โรงแรมริมทะเล",
        Description = descriptionText,
        DescriptionEmbedding = await GenerateEmbeddingAsync(descriptionText, embeddingGenerator),
        Tags = new[] { "วิวทะเล", "ครอบครัว" }
    });

    descriptionText = "โรงแรมบรรยากาศเงียบสงบ เหมาะสำหรับการพักผ่อน";
    hotels.Add(new Hotel
    {
        HotelId = hotelId++,
        HotelName = "โรงแรมเงียบสงบ",
        Description = descriptionText,
        DescriptionEmbedding = await GenerateEmbeddingAsync(descriptionText, embeddingGenerator),
        Tags = new[] { "ธรรมชาติ", "ส่วนตัว" }
    });

    descriptionText = "โรงแรมในเมือง สะดวกต่อการเดินทางและช้อปปิ้ง";
    hotels.Add(new Hotel
    {
        HotelId = hotelId++,
        HotelName = "โรงแรมในเมือง",
        Description = descriptionText,
        DescriptionEmbedding = await GenerateEmbeddingAsync(descriptionText, embeddingGenerator),
        Tags = new[] { "ในเมือง", "ใกล้แหล่งช้อปปิ้ง" }
    });

    descriptionText = "โรงแรมสไตล์รีสอร์ท พร้อมสวนและบรรยากาศร่มรื่น";
    hotels.Add(new Hotel
    {
        HotelId = hotelId++,
        HotelName = "โรงแรมสไตล์รีสอร์ท",
        Description = descriptionText,
        DescriptionEmbedding = await GenerateEmbeddingAsync(descriptionText, embeddingGenerator),
        Tags = new[] { "รีสอร์ท", "ธรรมชาติ" }
    });

    // Hotel 6-10
    descriptionText = "โรงแรมเล็ก ๆ ที่อบอุ่น เหมาะสำหรับคู่รัก";
    hotels.Add(new Hotel
    {
        HotelId = hotelId++,
        HotelName = "โรงแรมอบอุ่น",
        Description = descriptionText,
        DescriptionEmbedding = await GenerateEmbeddingAsync(descriptionText, embeddingGenerator),
        Tags = new[] { "โรแมนติก", "คู่รัก" }
    });

    descriptionText = "โรงแรมที่มีกิจกรรมกลางแจ้งหลากหลาย";
    hotels.Add(new Hotel
    {
        HotelId = hotelId++,
        HotelName = "โรงแรมกิจกรรมกลางแจ้ง",
        Description = descriptionText,
        DescriptionEmbedding = await GenerateEmbeddingAsync(descriptionText, embeddingGenerator),
        Tags = new[] { "กิจกรรม", "ครอบครัว" }
    });

    descriptionText = "โรงแรมหรูในใจกลางเมือง";
    hotels.Add(new Hotel
    {
        HotelId = hotelId++,
        HotelName = "โรงแรมหรู",
        Description = descriptionText,
        DescriptionEmbedding = await GenerateEmbeddingAsync(descriptionText, embeddingGenerator),
        Tags = new[] { "หรูหรา", "ใกล้สถานที่สำคัญ" }
    });

    descriptionText = "โรงแรมที่ให้ความสะดวกสบายในราคาประหยัด";
    hotels.Add(new Hotel
    {
        HotelId = hotelId++,
        HotelName = "โรงแรมราคาประหยัด",
        Description = descriptionText,
        DescriptionEmbedding = await GenerateEmbeddingAsync(descriptionText, embeddingGenerator),
        Tags = new[] { "ราคาประหยัด", "สะดวก" }
    });

    descriptionText = "โรงแรมสำหรับครอบครัวที่มีพื้นที่เด็กเล่น";
    hotels.Add(new Hotel
    {
        HotelId = hotelId++,
        HotelName = "โรงแรมสำหรับครอบครัว",
        Description = descriptionText,
        DescriptionEmbedding = await GenerateEmbeddingAsync(descriptionText, embeddingGenerator),
        Tags = new[] { "ครอบครัว", "พื้นที่เด็กเล่น" }
    });

    // Hotel 11-15
    descriptionText = "โรงแรมบูติกที่มีดีไซน์เฉพาะตัว";
    hotels.Add(new Hotel
    {
        HotelId = hotelId++,
        HotelName = "โรงแรมบูติกดีไซน์",
        Description = descriptionText,
        DescriptionEmbedding = await GenerateEmbeddingAsync(descriptionText, embeddingGenerator),
        Tags = new[] { "ดีไซน์", "บูติก" }
    });

    descriptionText = "โรงแรมใกล้สนามบิน สะดวกสำหรับนักเดินทาง";
    hotels.Add(new Hotel
    {
        HotelId = hotelId++,
        HotelName = "โรงแรมใกล้สนามบิน",
        Description = descriptionText,
        DescriptionEmbedding = await GenerateEmbeddingAsync(descriptionText, embeddingGenerator),
        Tags = new[] { "สนามบิน", "สะดวก" }
    });

    descriptionText = "โรงแรมในชนบท บรรยากาศธรรมชาติ";
    hotels.Add(new Hotel
    {
        HotelId = hotelId++,
        HotelName = "โรงแรมในชนบท",
        Description = descriptionText,
        DescriptionEmbedding = await GenerateEmbeddingAsync(descriptionText, embeddingGenerator),
        Tags = new[] { "ธรรมชาติ", "สงบ" }
    });

    descriptionText = "โรงแรมที่เน้นการบริการระดับพรีเมียม";
    hotels.Add(new Hotel
    {
        HotelId = hotelId++,
        HotelName = "โรงแรมพรีเมียม",
        Description = descriptionText,
        DescriptionEmbedding = await GenerateEmbeddingAsync(descriptionText, embeddingGenerator),
        Tags = new[] { "พรีเมียม", "หรูหรา" }
    });

    descriptionText = "โรงแรมที่เหมาะสำหรับจัดสัมมนาและงานประชุม";
    hotels.Add(new Hotel
    {
        HotelId = hotelId++,
        HotelName = "โรงแรมสัมมนา",
        Description = descriptionText,
        DescriptionEmbedding = await GenerateEmbeddingAsync(descriptionText, embeddingGenerator),
        Tags = new[] { "สัมมนา", "ห้องประชุม" }
    });

    // Hotel 16-20
    descriptionText = "โรงแรมที่มีอาหารเช้าฟรีและบริการยอดเยี่ยม";
    hotels.Add(new Hotel
    {
        HotelId = hotelId++,
        HotelName = "โรงแรมอาหารเช้าฟรี",
        Description = descriptionText,
        DescriptionEmbedding = await GenerateEmbeddingAsync(descriptionText, embeddingGenerator),
        Tags = new[] { "อาหารเช้าฟรี", "บริการดี" }
    });

    descriptionText = "โรงแรมที่เน้นความสะอาดและปลอดภัย";
    hotels.Add(new Hotel
    {
        HotelId = hotelId++,
        HotelName = "โรงแรมสะอาดและปลอดภัย",
        Description = descriptionText,
        DescriptionEmbedding = await GenerateEmbeddingAsync(descriptionText, embeddingGenerator),
        Tags = new[] { "สะอาด", "ปลอดภัย" }
    });

    descriptionText = "โรงแรมที่เป็นมิตรกับสัตว์เลี้ยง";
    hotels.Add(new Hotel
    {
        HotelId = hotelId++,
        HotelName = "โรงแรมที่รับสัตว์เลี้ยง",
        Description = descriptionText,
        DescriptionEmbedding = await GenerateEmbeddingAsync(descriptionText, embeddingGenerator),
        Tags = new[] { "สัตว์เลี้ยง", "ครอบครัว" }
    });

    descriptionText = "โรงแรมสำหรับนักเดินทางที่ชื่นชอบการผจญภัย";
    hotels.Add(new Hotel
    {
        HotelId = hotelId++,
        HotelName = "โรงแรมผจญภัย",
        Description = descriptionText,
        DescriptionEmbedding = await GenerateEmbeddingAsync(descriptionText, embeddingGenerator),
        Tags = new[] { "กิจกรรมกลางแจ้ง", "ผจญภัย" }
    });

    descriptionText = "โรงแรมที่ตั้งอยู่ในทำเลที่เงียบสงบและปลอดภัย";
    hotels.Add(new Hotel
    {
        HotelId = hotelId++,
        HotelName = "โรงแรมเงียบสงบ",
        Description = descriptionText,
        DescriptionEmbedding = await GenerateEmbeddingAsync(descriptionText, embeddingGenerator),
        Tags = new[] { "สงบ", "ปลอดภัย" }
    });

    return hotels;
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

static async Task<IVectorStoreRecordCollection<ulong, Hotel>> CreateQdrantCollection(QdrantVectorStore vectorStore)
{
    var collection = vectorStore.GetCollection<ulong, Hotel>("skhotels");

    // Create the collection if it doesn't exist yet.
    await collection.CreateCollectionIfNotExistsAsync();

    return collection;
}

static async Task UpsertPoints(IVectorStoreRecordCollection<ulong, Hotel> collection, List<Hotel> hotels)
{
    // Upsert all hotels to the collection
    foreach (var hotel in hotels)
    {
        await collection.UpsertAsync(hotel);
    }
}

static async Task TrytoGetAndDisplayaRecordWithoutVectorSearch(IVectorStoreRecordCollection<ulong, Hotel> collection, ulong hotelId)
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

static async Task PerformVectorSearch(IVectorStoreRecordCollection<ulong, Hotel> collection, string? userPrompt, ITextEmbeddingGenerationService embeddingGenerator)
{
    // Generate a vector for your search text, using your chosen embedding generation implementation.
    ReadOnlyMemory<float> searchVector = await GenerateEmbeddingAsync(userPrompt, embeddingGenerator);

    // Do the search.
    var searchResult = await collection.VectorizedSearchAsync(searchVector, new() { Top = 5, IncludeTotalCount = true });

    // Inspect the returned hotel.
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
    [VectorStoreRecordKey]
    public ulong HotelId { get; set; }

    [VectorStoreRecordData(IsFilterable = true)]
    public string HotelName { get; set; }

    [VectorStoreRecordData(IsFullTextSearchable = true)]
    public string Description { get; set; }

    [VectorStoreRecordVector(Dimensions: EmbeddingDimensions.OpenAIEmbeddingSize, DistanceFunction.CosineSimilarity)]
    public ReadOnlyMemory<float>? DescriptionEmbedding { get; set; }

    [VectorStoreRecordData(IsFilterable = true)]
    public string[] Tags { get; set; }
}

internal class EmbeddingDimensions
{
    public const int OllamaEmbeddingSize = 384;
    public const int OpenAIEmbeddingSize = 1536;
}