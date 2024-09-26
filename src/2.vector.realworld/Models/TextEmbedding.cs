using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;
using System.Collections.Generic;

namespace VectorApp.Models
{
    public class TextEmbedding
    {
        [BsonId]
        [BsonRepresentation(BsonType.ObjectId)]
        public string? Id { get; set; }

        [BsonElement("text")]
        public string? Text { get; set; }

        [BsonElement("processedText")]
        public string? ProcessedText { get; set; }

        [BsonElement("embedding")]
        public List<double>? Embedding { get; set; }
    }
}
