﻿using System.Text;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.Connectors.MongoDB;
using Microsoft.SemanticKernel.Embeddings;
using Microsoft.SemanticKernel.Memory;
using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;
using MongoDB.Driver;

#pragma warning disable SKEXP0001, SKEXP0010, SKEXP0020, SKEXP0050

namespace HelloVector
{
    class Program
    {
        static async Task Main(string[] args)
        {
            Console.OutputEncoding = System.Text.Encoding.UTF8;
            
            string chatModel, textEmbeddingModel, azureAPIEndpoint, azureAPIKey;
            string mongodbConnectionString, mongodbSearchIndexName, mongodbDatabaseName, mongodbCollectionName;

            ReadDataFromConfig(out chatModel, out textEmbeddingModel, out azureAPIEndpoint, out azureAPIKey,out mongodbConnectionString, out mongodbSearchIndexName, out mongodbDatabaseName, out mongodbCollectionName);

            Kernel kernel = SetUpSemanticKernel(chatModel, textEmbeddingModel, azureAPIEndpoint, azureAPIKey);

            SemanticTextMemory memory = SetupSemanticMemoryWithMongoDBStore(mongodbConnectionString, mongodbSearchIndexName, mongodbDatabaseName, kernel);

            // Uncomment the following line to convert data to vector and save in MongoDB
            // Run this line only once to avoid inserting duplicate data into the MongoDB collection
            //await ConvertDataToVectorAndSaveInMongoDB(memory, 100, mongodbConnectionString, mongodbCollectionName);

            await RAGInMongoDB(kernel, mongodbCollectionName, memory);
        }
        private static void ReadDataFromConfig(out string chatModel, out string textEmbeddingModel, out string azureAPIEndpoint, out string azureAPIKey,out string mongodbConnectionString, out string mongodbSearchIndexName, out string mongodbDatabaseName, out string mongodbCollectionName)
        {
            IConfiguration configuration = new ConfigurationBuilder()
                            .AddJsonFile("appsettings.json")
                            .Build();

            chatModel = configuration["AzureOpenAI:ChatModel"] ?? throw new ArgumentNullException("AzureOpenAI:ChatModel");
            textEmbeddingModel = configuration["AzureOpenAI:TextEmbeddingModel"] ?? throw new ArgumentNullException("AzureOpenAI:TextEmbeddingModel");
            azureAPIEndpoint = configuration["AzureOpenAI:Endpoint"] ?? throw new ArgumentNullException("AzureOpenAI:Endpoint");
            azureAPIKey = configuration["AzureOpenAI:ApiKey"] ?? throw new ArgumentNullException("AzureOpenAI:ApiKey");

            mongodbConnectionString = configuration["MongoDB:ConnectionString"] ?? throw new ArgumentNullException("MongoDB:ConnectionString");
            mongodbSearchIndexName = configuration["MongoDB:SearchIndexName"] ?? throw new ArgumentNullException("MongoDB:SearchIndexName");
            mongodbDatabaseName = configuration["MongoDB:DatabaseName"] ?? throw new ArgumentNullException("MongoDB:DatabaseName");
            mongodbCollectionName = configuration["MongoDB:CollectionName"] ?? throw new ArgumentNullException("MongoDB:CollectionName");
        }
        private static Kernel SetUpSemanticKernel(string chatModel, string textEmbeddingModel, string azureAPIEndpoint, string azureAPIKey)
        {
            var builder = Kernel.CreateBuilder();

            // Setup Chat Completion
            builder.AddAzureOpenAIChatCompletion(
                            deploymentName: chatModel,
                            endpoint: azureAPIEndpoint,
                            apiKey: azureAPIKey
                        );

            // Setup Text Embedding
            builder.AddAzureOpenAITextEmbeddingGeneration(
                            deploymentName: textEmbeddingModel,
                            endpoint: azureAPIEndpoint,
                            apiKey: azureAPIKey
                        );

            return builder.Build();
        }
        private static SemanticTextMemory SetupSemanticMemoryWithMongoDBStore(string mongodbConnectionString, string mongodbSearchIndexName, string mongodbDatabaseName, Kernel kernel)
        {
            // Setup the MongoDB memory store
            var mongoDBMemoryStore = new MongoDBMemoryStore(mongodbConnectionString, mongodbDatabaseName, mongodbSearchIndexName);

            // get the embeddings generator service
            var embeddingGenerator = kernel.Services.GetRequiredService<ITextEmbeddingGenerationService>();
            var memory = new SemanticTextMemory(mongoDBMemoryStore, embeddingGenerator);
            return memory;
        }
        private static async Task ConvertDataToVectorAndSaveInMongoDB(ISemanticTextMemory memory, int limitSize, string mongodbConnectionString, string mongodbCollectionName)
        {
            Console.WriteLine("Memory is ready...");
            Console.WriteLine("Please wait while we fetch and save movie documents to the memory store..");

            MongoClient mongoClient = new MongoClient(mongodbConnectionString);
            var movieDB = mongoClient.GetDatabase("sample_mflix");
            var movieCollection = movieDB.GetCollection<Movie>("movies");
            List<Movie> movieDocuments;

            Console.WriteLine("-- Fetching documents from MongoDB...");

            movieDocuments = movieCollection.Find(m => m.Fullplot != null).Limit(limitSize).ToList();

            foreach (var movie in movieDocuments)
            {
                try
                {
                    Console.WriteLine($"-- Converting movie to vector and save in MongoDB: {movie.Title}");
                    
                    await memory.SaveReferenceAsync(
                        collection: mongodbCollectionName,
                        description: movie.Fullplot,
                        text: movie.Fullplot,
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
        private static async Task RAGInMongoDB(Kernel kernel, string mongodbCollectionName, SemanticTextMemory memory)
        {
            DisplayWelcomeRAG();

            while (true)
            {
                string? userInput = GetUserMovieInput();

                if (userInput.ToLower() == "x")
                {
                    Console.WriteLine("Exiting application..");
                    break;
                }

                StringBuilder prompts = await GetMovieCategoryFromUserPrompt(kernel, userInput);

                StringBuilder relevantFacts = await PerformVectorSearchAndDisplay(mongodbCollectionName, memory, prompts);

                var isDataFound = relevantFacts.Length > 0;
                if (!isDataFound)
                {
                    Console.WriteLine("ไม่พบข้อมูลหนังที่ตรงกับความต้องการของคุณ");
                    continue;
                }

                await DisplayRelatedMoviesSummary(kernel, prompts, relevantFacts);
            }
        }
        private static void DisplayWelcomeRAG()
        {
            Console.WriteLine("Welcome to the Movie Recommendation System!");
            Console.WriteLine("Type 'x' and press Enter to exit.");
            Console.WriteLine("============================================");
            Console.WriteLine();
        }
        private static string GetUserMovieInput()
        {
            Console.WriteLine();
            Console.WriteLine("Tell me what sort of film you want to watch..");
            Console.WriteLine();

            Console.Write("> ");

            return Console.ReadLine();
        }
        private static async Task<StringBuilder> GetMovieCategoryFromUserPrompt(Kernel kernel, string userInput)
        {
            var userInputResponses = kernel.InvokePromptStreamingAsync("Convert to only the movie genre in English.: " + userInput);

            StringBuilder prompts = new StringBuilder();
            await foreach (var inputResponse in userInputResponses)
            {
                prompts.Append(inputResponse);
            }

            Console.WriteLine();
            Console.WriteLine($"ประเภทหนังที่คุณเลือกคือ: {prompts.ToString()}");
            Console.WriteLine();
            Console.WriteLine();
            return prompts;
        }
        private static async Task<StringBuilder> PerformVectorSearchAndDisplay(string mongodbCollectionName, SemanticTextMemory memory, StringBuilder prompts)
        {
            var memories = memory.SearchAsync(mongodbCollectionName, prompts.ToString(), limit: 3, minRelevanceScore: 0.6);

            Console.WriteLine(String.Format("{0,-20} {1,-50} {2,-10} {3,-15}", "Title", "Plot", "Year", "Relevance (0 - 1)"));
            Console.WriteLine(new String('-', 95)); // Adjust the length based on your column widths

            StringBuilder relevantFacts = new StringBuilder();
            await foreach (var mem in memories)
            {
                Console.WriteLine(String.Format("{0,-20} {1,-50} {2,-10} {3,-15}",
                    mem.Metadata.Id,
                    mem.Metadata.Description.Length > 47 ? mem.Metadata.Description.Substring(0, 47) + "..." : mem.Metadata.Description, // Truncate long descriptions
                    mem.Metadata.AdditionalMetadata,
                    mem.Relevance.ToString("0.00"))); // Format relevance score to two decimal places

                relevantFacts.AppendLine($"ชื่อหนัง: {mem.Metadata.Id}, รายละเอียด: {mem.Metadata.Description}");
            }

            Console.WriteLine();
            Console.WriteLine();

            return relevantFacts;
        }
        private static async Task DisplayRelatedMoviesSummary(Kernel kernel, StringBuilder prompts, StringBuilder relevantFacts)
        {
            var prompt = $@"
                    Question: สรุปชื่อหนังและเนื้อหาของหนังว่าเกี่ยวกับอะไร เป็นภาษาไทย

                    Answer the question using the memory content:

                    ความต้องการดูหนังประเภท: {prompts.ToString()}

                    ข้อมูลที่เกี่ยวข้อง:   
                    {relevantFacts.ToString()}

                    Answer:";

            var response = kernel.InvokePromptStreamingAsync(prompt);
            await foreach (var result in response)
            {
                Console.Write(result);
            }

            Console.WriteLine();
            Console.WriteLine();
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
}