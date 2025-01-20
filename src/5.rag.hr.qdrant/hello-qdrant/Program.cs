using Qdrant.Client;
using Qdrant.Client.Grpc;
using static Qdrant.Client.Grpc.Conditions;

var client = CreateClient(isLocal: true);
//var client = CreateClient(isLocal: false, host: "your api url" apiKey: "your api key");

await CreateCollection(client);
await UpsertPoints(client);
await RunSimpleSearch(client);
await RunFilterSearch(client);

static async Task CreateCollection(QdrantClient client)
{
    var collection = "test_collection";

    if (await client.CollectionExistsAsync(collection))
        return;

    await client.CreateCollectionAsync(collectionName: collection, vectorsConfig: new VectorParams
    {
        Size = 4,
        Distance = Distance.Dot
    });
}

static async Task UpsertPoints(QdrantClient client)
{
    var operationInfo = await client.UpsertAsync(collectionName: "test_collection", points: new List<PointStruct>
    {
        new()
        {
            Id = 1,
                Vectors = new float[]
                {
                    0.05f, 0.61f, 0.76f, 0.74f
                },
                Payload = {
                    ["city"] = "Berlin"
                }
        },
        new()
        {
            Id = 2,
                Vectors = new float[]
                {
                    0.19f, 0.81f, 0.75f, 0.11f
                },
                Payload = {
                    ["city"] = "London"
                }
        },
        new()
        {
            Id = 3,
                Vectors = new float[]
                {
                    0.36f, 0.55f, 0.47f, 0.94f
                },
                Payload = {
                    ["city"] = "Moscow"
                }
        },
        // Truncated
    });

    Console.WriteLine();
    Console.WriteLine("Upsert operation info:");
    Console.WriteLine("----------------------");
    Console.WriteLine(operationInfo);
    Console.WriteLine();
}

static async Task RunSimpleSearch(QdrantClient client)
{
    var searchResult = await client.QueryAsync(
        collectionName: "test_collection",
        query: new float[] { 0.2f, 0.1f, 0.9f, 0.7f },
        limit: 3
    );

    Console.WriteLine("Simple search result:");
    Console.WriteLine("---------------------");
    Console.WriteLine(searchResult);
    Console.WriteLine();
}

static async Task RunFilterSearch(QdrantClient client)
{
    var searchResult = await client.QueryAsync(
        collectionName: "test_collection",
        query: new float[] { 0.2f, 0.1f, 0.9f, 0.7f },
        filter: MatchKeyword("city", "London"),
        limit: 3,
        payloadSelector: true
    );

    Console.WriteLine("Filter search result:");
    Console.WriteLine("----------------------");
    Console.WriteLine(searchResult);
    Console.WriteLine();
}

static QdrantClient CreateClient(bool isLocal = true, string host = "", string apiKey = "")
{
    if (isLocal)
        return new QdrantClient(
            host: "localhost", 
            port: 6334);

    return new QdrantClient(
      host: host,
      https: true,
      apiKey: apiKey
    );
}