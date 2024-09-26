using System;
using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace VectorApp.Models
{
    public class LineEvent
    {
        [JsonPropertyName("destination")]
        public string? Destination { get; set; }

        [JsonPropertyName("events")]
        public List<Event>? Events { get; set; }
    }

    public class Event
    {
        [JsonPropertyName("type")]
        public string? Type { get; set; }

        [JsonPropertyName("message")]
        public Message? Message { get; set; }

        [JsonPropertyName("webhookEventId")]
        public string? WebhookEventId { get; set; }

        [JsonPropertyName("deliveryContext")]
        public DeliveryContext? DeliveryContext { get; set; }

        [JsonPropertyName("timestamp")]
        public long? Timestamp { get; set; }

        [JsonPropertyName("source")]
        public Source? Source { get; set; }

        [JsonPropertyName("replyToken")]
        public string? ReplyToken { get; set; }

        [JsonPropertyName("mode")]
        public string? Mode { get; set; }
    }

    public class Message
    {
        [JsonPropertyName("type")]
        public string? Type { get; set; }

        [JsonPropertyName("id")]
        public string? Id { get; set; }

        [JsonPropertyName("quoteToken")]
        public string? QuoteToken { get; set; }

        [JsonPropertyName("text")]
        public string? Text { get; set; }
    }

    public class DeliveryContext
    {
        [JsonPropertyName("isRedelivery")]
        public bool? IsRedelivery { get; set; }
    }

    public class Source
    {
        [JsonPropertyName("type")]
        public string? Type { get; set; }

        [JsonPropertyName("userId")]
        public string? UserId { get; set; }
    }
}
