using Microsoft.SemanticKernel.Connectors.MongoDB;
using Microsoft.SemanticKernel.Connectors.OpenAI;
using Microsoft.SemanticKernel.Memory;
using MongoDB.Driver;
using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;

#pragma warning disable SKEXP0001, SKEXP0010, SKEXP0020, SKEXP0050
public static class Program
{
    static string TextEmbeddingModelName = "text-embedding-3-small";
    static string OpenAIAPIKey = "";

    static string MongoDBAtlasConnectionString = "";
    static string SearchIndexName = "default";
    static string DatabaseName = "semantic-kernel";
    static string CollectionName = "movies";
    static MemoryBuilder memoryBuilder;

    public static async Task Main(string[] args)
    {
        Console.OutputEncoding = System.Text.Encoding.UTF8;
        
        memoryBuilder = new MemoryBuilder();
        memoryBuilder.WithOpenAITextEmbeddingGeneration(
            TextEmbeddingModelName,
            OpenAIAPIKey
            );

        var mongoDBMemoryStore = new MongoDBMemoryStore(MongoDBAtlasConnectionString, DatabaseName, SearchIndexName);
        memoryBuilder.WithMemoryStore(mongoDBMemoryStore);
        var memory = memoryBuilder.Build();

        Console.WriteLine("Memory is ready...");

        //await FetchAndSaveMovieDocuments(memory, 100);

        Console.WriteLine("Welcome to the Movie Recommendation System!");
        Console.WriteLine("Type 'x' and press Enter to exit.");
        Console.WriteLine("============================================");
        Console.WriteLine();

        while(true)
        {
            Console.WriteLine("Tell me what sort of film you want to watch..");
            Console.WriteLine();

            Console.Write("> ");

            var userInput = Console.ReadLine();

            if(userInput.ToLower() == "x")
            {
                Console.WriteLine("Exiting application..");
                break;
            }

            Console.WriteLine();

            var memories = memory.SearchAsync(CollectionName, userInput, limit: 3, minRelevanceScore: 0.6);

            Console.WriteLine(String.Format("{0,-20} {1,-50} {2,-10} {3,-15}", "Title", "Plot", "Year", "Relevance (0 - 1)"));
            Console.WriteLine(new String('-', 95)); // Adjust the length based on your column widths

            await foreach (var mem in memories)
            {
                Console.WriteLine(String.Format("{0,-20} {1,-50} {2,-10} {3,-15}", 
                    mem.Metadata.Id, 
                    mem.Metadata.Description.Length > 47 ? mem.Metadata.Description.Substring(0, 47) + "..." : mem.Metadata.Description, // Truncate long descriptions
                    mem.Metadata.AdditionalMetadata, 
                    mem.Relevance.ToString("0.00"))); // Format relevance score to two decimal places
            }
        }

    }

    private static async Task FetchAndSaveMovieDocuments(ISemanticTextMemory memory, int limitSize)
    {
        MongoClient mongoClient = new MongoClient(MongoDBAtlasConnectionString);
        var movieDB = mongoClient.GetDatabase("sample_mflix");
        var movieCollection = movieDB.GetCollection<Movie>("movies");
        List<Movie> movieDocuments;

        Console.WriteLine("Fetching documents from MongoDB...");

        movieDocuments = movieCollection.Find(m => m.Plot != null).Limit(limitSize).ToList();

        foreach (var movie in movieDocuments)
        {
            try
            {
                await memory.SaveReferenceAsync(
                collection: CollectionName,
                description: movie.Plot,
                text: movie.Plot,
                externalId: movie.Title,
                externalSourceName: "Sample_Mflix_Movies",
                additionalMetadata: movie.Year.ToString());
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex.Message);

            }
        }
    }

    public class Movie
    {
        [BsonId]
        [BsonRepresentation(BsonType.ObjectId)]
        public string Id { get; set; }

        [BsonElement("plot")]
        public string Plot { get; set; }

        [BsonElement("genres")]
        public List<string> Genres { get; set; }

        [BsonElement("runtime")]
        public int Runtime { get; set; }

        [BsonElement("cast")]
        public List<string> Cast { get; set; }

        [BsonElement("num_mflix_comments")]
        public int NumMflixComments { get; set; }

        [BsonElement("poster")]
        public string Poster { get; set; }

        [BsonElement("title")]
        public string Title { get; set; }

        [BsonElement("fullplot")]
        public string Fullplot { get; set; }

        [BsonElement("languages")]
        public List<string> Languages { get; set; }

        [BsonElement("released")]
        public DateTime Released { get; set; }

        [BsonElement("directors")]
        public List<string> Directors { get; set; }

        [BsonElement("writers")]
        public List<string> Writers { get; set; }

        [BsonElement("awards")]
        public Awards Awards { get; set; }

        [BsonElement("rated")]
        public string? Rated { get; set; }

        [BsonElement("lastupdated")]
        public string Lastupdated { get; set; }


        [BsonElement("year")]
        public object Year { get; set; }

        [BsonElement("imdb")]
        public Imdb Imdb { get; set; }

        [BsonElement("countries")]
        public List<string> Countries { get; set; }

        [BsonElement("type")]
        public string Type { get; set; }

        [BsonElement("tomatoes")]
        public Tomatoes Tomatoes { get; set; }

        [BsonElement("metacritic")]
        public int? Metacritic { get; set; }

        [BsonElement("awesome")]
        public bool? Awesome { get; set; }
    }

    public class Awards
    {
        [BsonElement("wins")]
        public int Wins { get; set; }

        [BsonElement("nominations")]
        public int Nominations { get; set; }

        [BsonElement("text")]
        public string Text { get; set; }
    }

    public class Imdb
    {
        [BsonElement("id")]
        public object ImdbId { get; set; }

        [BsonElement("votes")]
        public object Votes { get; set; }

        [BsonElement("rating")]
        public object Rating { get; set; }
    }

    public class Tomatoes
    {
        [BsonElement("viewer")]
        public Viewer Viewer { get; set; }

        [BsonElement("lastUpdated")]
        public DateTime LastUpdated { get; set; }

        [BsonElement("dvd")]
        public DateTime? DVD { get; set; }

        [BsonElement("website")]
        public string? Website { get; set; }

        [BsonElement("production")]
        public string? Production { get; set; }

        [BsonElement("critic")]
        public Critic? Critic { get; set; }

        [BsonElement("rotten")]
        public int? Rotten { get; set; }

        [BsonElement("fresh")]
        public int? Fresh { get; set; }

        [BsonElement("boxOffice")]
        public string? BoxOffice { get; set; }

        [BsonElement("consensus")]
        public string? Consensus { get; set; }

    }

    public class Viewer
    {
        [BsonElement("rating")]
        public double Rating { get; set; }

        [BsonElement("numReviews")]
        public int NumReviews { get; set; }

        [BsonElement("meter")]
        public int Meter { get; set; }
    }

    public class Critic
    {
        [BsonElement("rating")]
        public double Rating { get; set; }

        [BsonElement("numReviews")]
        public int NumReviews { get; set; }

        [BsonElement("meter")]
        public int Meter { get; set; }
    }

}
