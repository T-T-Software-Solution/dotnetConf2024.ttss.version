using Microsoft.Extensions.Configuration;
using MongoDB.Bson;
using MongoDB.Driver;
using VectorApp.Models;
using System.Collections.Generic;
using System.Threading.Tasks;
using System.Collections;

namespace VectorApp.Services
{
    public class MongoDbService
    {
        private readonly IMongoCollection<TextEmbedding> _textEmbeddingCollection;
        private readonly IMongoCollection<BsonDocument> _travelEmbeddingCollection;
        private readonly IMongoDatabase _database;
        private readonly string _collectionName;
        private readonly OpenAiService _openAiService;
        private readonly MongoClient _client;

        public MongoDbService(IConfiguration config, OpenAiService openAiService)
        {
            _client = new MongoClient(config["MongoDB:ConnectionString"]);
            _database = _client.GetDatabase(config["MongoDB:DatabaseName"]);
            _collectionName = config["MongoDB:CollectionName"];
            _textEmbeddingCollection = _database.GetCollection<TextEmbedding>(_collectionName);
            _openAiService = openAiService; // Inject the OpenAI service
            _travelEmbeddingCollection = _database.GetCollection<BsonDocument>(config["MongoDB:TravelCollectionName"]);
        }

        // Property to check if the MongoDB connection is established
        public bool IsConnected
        {
            get
            {
                try
                {
                    // Check the server status by pinging the database
                    _client.ListDatabaseNames(); // If this does not throw an exception, the connection is valid
                    return true;
                }
                catch (Exception)
                {
                    // If there is any exception, the connection is not established
                    return false;
                }
            }
        }
        public async Task<List<TextEmbedding>> SearchSimilarTextsAsync(List<double> queryEmbedding, int topK)
        {
            // Create the aggregation pipeline for vector similarity search
            var pipeline = new BsonDocument[]
            {
                new BsonDocument
                {
                    {
                        "$vectorSearch",
                        new BsonDocument
                        {
                            { "index", "embedding_vectorSearch" },  // Name of the vector search index created in MongoDB
                            { "path", "embedding" },  // Field path to the stored embeddings in MongoDB
                            { "queryVector", new BsonArray(queryEmbedding) },  // Query embedding vector
                            { "limit", topK },  // Number of top results to return
                            { "numCandidates", 100 },  // Number of candidates to consider (adjust based on performance requirements)
                            { "exact", false }  // Set to true for exact matches, false for approximate matches
                        }
                    }
                },
                new BsonDocument
                {
                    {
                        "$project",
                        new BsonDocument
                        {
                            { "text", 1 }, // Include the text field in the results
                            { "score", 1 }  // Include the similarity score in the results
                        }
                    }
                }
            };

            // Execute the aggregation pipeline
            var results = await _textEmbeddingCollection.AggregateAsync<TextEmbedding>(pipeline);
            return await results.ToListAsync();
        }

        public async Task<List<TravelEmbeddingResult>> SearchByAnalysisAndSimilarityAsync(TravelAnalysisResult analysisResult, List<double> queryEmbedding, int topK)
        {
            // Step 1: Create a filter based on the TravelAnalysisResult properties, ignoring null or empty values
            var filters = new List<FilterDefinition<BsonDocument>>();

            if (!string.IsNullOrEmpty(analysisResult.Region))
            {
                filters.Add(Builders<BsonDocument>.Filter.Regex("REGION_NAME_TH", new BsonRegularExpression(analysisResult.Region, "i")));
            }

            if (!string.IsNullOrEmpty(analysisResult.Province))
            {
                filters.Add(Builders<BsonDocument>.Filter.Regex("PROVINCE_NAME_TH", new BsonRegularExpression(analysisResult.Province, "i")));
            }

            if (!string.IsNullOrEmpty(analysisResult.District))
            {
                filters.Add(Builders<BsonDocument>.Filter.Regex("DISTRICT_NAME_TH", new BsonRegularExpression(analysisResult.District, "i")));
            }

            if (!string.IsNullOrEmpty(analysisResult.PlaceType))
            {
                filters.Add(Builders<BsonDocument>.Filter.Regex("ATTR_SUB_TYPE_TH", new BsonRegularExpression(analysisResult.PlaceType, "i")));
            }

            if (!string.IsNullOrEmpty(analysisResult.Month))
            {
                filters.Add(Builders<BsonDocument>.Filter.Regex("EXT_SUGGEST_MONTH", new BsonRegularExpression(analysisResult.Month, "i")));
            }

            if (!string.IsNullOrEmpty(analysisResult.Day))
            {
                filters.Add(Builders<BsonDocument>.Filter.Regex("EXT_SUGGEST_DAY", new BsonRegularExpression(analysisResult.Day, "i")));
            }

            if (!string.IsNullOrEmpty(analysisResult.Time))
            {
                filters.Add(Builders<BsonDocument>.Filter.Regex("EXT_SUGGEST_TIME", new BsonRegularExpression(analysisResult.Time, "i")));
            }

            // Combine all filters with 'AND' condition, ignore if no filters are applied
            var combinedFilter = filters.Any() ? Builders<BsonDocument>.Filter.And(filters) : FilterDefinition<BsonDocument>.Empty;

            // Step 2: Create an aggregation pipeline with vector search as the first stage
            var pipeline = new List<BsonDocument>
    {
        new BsonDocument
        {
            {
                "$vectorSearch",
                new BsonDocument
                {
                    { "index", "travel_vector_index" },  // Use the travel embedding index
                    { "path", "embedding" },  // Field path to the stored embeddings in MongoDB
                    { "queryVector", new BsonArray(queryEmbedding) },  // Query embedding vector
                    { "limit", 1000 },  // Use a high limit to get a comprehensive result set from the vector search
                    { "numCandidates", 1000 },  // Set a high number of candidates to get more results
                    { "exact", false }  // Set to true for exact matches, false for approximate matches
                }
            }
        }
    };

            // Step 3: Add a match stage only if there are additional filters
            if (filters.Any())
            {
                // Render combinedFilter to BsonDocument
                var matchStage = new BsonDocument
        {
            { "$match", combinedFilter.Render(
                _travelEmbeddingCollection.DocumentSerializer,
                _travelEmbeddingCollection.Settings.SerializerRegistry) }
        };
                pipeline.Add(matchStage);
            }

            // Step 4: Sort by the score to get the top results based on similarity
            pipeline.Add(new BsonDocument
    {
        { "$sort", new BsonDocument("score", -1) }  // Sort by score in descending order
    });

            // Step 5: Limit the results to the topK
            pipeline.Add(new BsonDocument
    {
        { "$limit", topK }  // Apply topK to the final filtered and sorted results
    });

            // Step 6: Add a projection stage to include required fields
            pipeline.Add(new BsonDocument
    {
        {
            "$project",
            new BsonDocument
            {
                { "ATT_NAME_TH", 1 },  // Include the required fields in the results
                { "ATT_NAME_EN", 1 },
                { "ATTR_SUB_TYPE_TH", 1 },
                { "ATT_START_END", 1 },
                { "ATT_FEE_TH", 1 },
                { "ATT_FEE_TH_KID", 1 },
                { "ATT_FEE_EN", 1 },
                { "ATT_DETAIL_TH", 1 },
                { "PROVINCE_NAME_TH", 1 },
                { "DISTRICT_NAME_TH", 1 },
                { "ATT_FACILITIES_CONTACT", 1 },
                { "ATT_WEBSITE", 1 },
                { "ATT_FACEBOOK", 1 },
                { "ATT_LOCATION", 1 },
                { "score", 1 }  // Include the similarity score
            }
        }
    });

            // Execute the aggregation pipeline
            var results = await _travelEmbeddingCollection.AggregateAsync<BsonDocument>(pipeline);
            var travelEmbeddingResults = new List<TravelEmbeddingResult>();

            await results.ForEachAsync(result =>
            {
                var generalDescription = new GeneralDescription
                {
                    // Check if the field exists and is of the correct type before accessing
                    Name = result.Contains("ATT_NAME_TH") && result["ATT_NAME_TH"].IsString ? result["ATT_NAME_TH"].AsString : string.Empty,
                    Type = result.Contains("ATTR_SUB_TYPE_TH") && result["ATTR_SUB_TYPE_TH"].IsString ? result["ATTR_SUB_TYPE_TH"].AsString : string.Empty,
                    OpenCloseTime = result.Contains("ATT_START_END") && result["ATT_START_END"].IsString ? result["ATT_START_END"].AsString : string.Empty,
                    EntryFee = result.Contains("ATT_FEE_TH") && result["ATT_FEE_TH"].IsString ? result["ATT_FEE_TH"].AsString : string.Empty +
                               (result.Contains("ATT_FEE_TH_KID") && result["ATT_FEE_TH_KID"].IsString ? " เด็ก " + result["ATT_FEE_TH_KID"].AsString : string.Empty) +
                               (result.Contains("ATT_FEE_EN") && result["ATT_FEE_EN"].IsString ? " ชาวต่างชาติ " + result["ATT_FEE_EN"].AsString : string.Empty),
                    Detail = result.Contains("ATT_DETAIL_TH") && result["ATT_DETAIL_TH"].IsString ? result["ATT_DETAIL_TH"].AsString : string.Empty,
                    Location = result.Contains("PROVINCE_NAME_TH") && result["PROVINCE_NAME_TH"].IsString ? result["PROVINCE_NAME_TH"].AsString : string.Empty +
                               (result.Contains("DISTRICT_NAME_TH") && result["DISTRICT_NAME_TH"].IsString ? " " + result["DISTRICT_NAME_TH"].AsString : string.Empty),
                    Contact = result.Contains("ATT_FACILITIES_CONTACT") && result["ATT_FACILITIES_CONTACT"].IsString ? result["ATT_FACILITIES_CONTACT"].AsString : string.Empty +
                              (result.Contains("ATT_WEBSITE") && result["ATT_WEBSITE"].IsString ? " " + result["ATT_WEBSITE"].AsString : string.Empty) +
                              (result.Contains("ATT_FACEBOOK") && result["ATT_FACEBOOK"].IsString ? " " + result["ATT_FACEBOOK"].AsString : string.Empty)
                };

                travelEmbeddingResults.Add(new TravelEmbeddingResult
                {
                    GeneralDescription = generalDescription,
                    ATT_LOCATION = result.Contains("ATT_LOCATION") && result["ATT_LOCATION"].IsString ? result["ATT_LOCATION"].AsString : string.Empty,
                    ATT_WEBSITE = result.Contains("ATT_WEBSITE") && result["ATT_WEBSITE"].IsString ? result["ATT_WEBSITE"].AsString : string.Empty,
                    ATT_FACEBOOK = result.Contains("ATT_FACEBOOK") && result["ATT_FACEBOOK"].IsString ? result["ATT_FACEBOOK"].AsString : string.Empty,
                    Score = result.Contains("score") && result["score"].IsDouble ? result["score"].AsDouble : 0.0  // Handle score correctly
                });
            });

            return travelEmbeddingResults;
        }

    }
}
