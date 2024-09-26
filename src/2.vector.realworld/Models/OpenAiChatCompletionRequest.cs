using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace VectorApp.Models
{
    public class OpenAiChatCompletionRequest
    {
        [JsonPropertyName("model")]
        public string? Model { get; set; }

        [JsonPropertyName("messages")]
        public List<ChatMessage>? Messages { get; set; }

        [JsonPropertyName("temperature")]
        public double Temperature { get; set; }
    }

    public class ChatMessage
    {
        [JsonPropertyName("role")]
        public string? Role { get; set; }

        [JsonPropertyName("content")]
        public string? Content { get; set; }
    }
}
