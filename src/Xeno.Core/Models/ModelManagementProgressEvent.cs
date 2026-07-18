namespace Xeno.Core.Models
{
    using System;
    using System.Text.Json.Serialization;

    /// <summary>
    /// Dashboard-facing model management progress event.
    /// </summary>
    public class ModelManagementProgressEvent
    {
        /// <summary>
        /// Stable progress event type.
        /// </summary>
        [JsonPropertyName("eventType")]
        public string EventType { get; set; } = "progress";

        /// <summary>
        /// Xeno request ID.
        /// </summary>
        [JsonPropertyName("requestId")]
        public string RequestId { get; set; } = String.Empty;

        /// <summary>
        /// Client ID.
        /// </summary>
        [JsonPropertyName("clientId")]
        public string ClientId { get; set; } = String.Empty;

        /// <summary>
        /// Operation being executed.
        /// </summary>
        [JsonPropertyName("operation")]
        public string Operation { get; set; } = String.Empty;

        /// <summary>
        /// Model name.
        /// </summary>
        [JsonPropertyName("model")]
        public string Model { get; set; } = String.Empty;

        /// <summary>
        /// Client-local provider source name.
        /// </summary>
        [JsonPropertyName("source")]
        public string? Source { get; set; } = null;

        /// <summary>
        /// Provider or server status text.
        /// </summary>
        [JsonPropertyName("status")]
        public string? Status { get; set; } = null;

        /// <summary>
        /// Human-readable message.
        /// </summary>
        [JsonPropertyName("message")]
        public string Message { get; set; } = String.Empty;

        /// <summary>
        /// Bytes completed when available.
        /// </summary>
        [JsonPropertyName("completed")]
        public long? Completed { get; set; } = null;

        /// <summary>
        /// Total bytes when available.
        /// </summary>
        [JsonPropertyName("total")]
        public long? Total { get; set; } = null;

        /// <summary>
        /// Layer digest when available.
        /// </summary>
        [JsonPropertyName("digest")]
        public string? Digest { get; set; } = null;

        /// <summary>
        /// Raw provider progress line when available.
        /// </summary>
        [JsonPropertyName("rawLine")]
        public string? RawLine { get; set; } = null;

        /// <summary>
        /// Final response when the operation has completed.
        /// </summary>
        [JsonPropertyName("result")]
        public ClientModelManagementResponseMessage? Result { get; set; } = null;

        /// <summary>
        /// UTC timestamp when emitted.
        /// </summary>
        [JsonPropertyName("sentUtc")]
        public DateTime SentUtc { get; set; } = DateTime.UtcNow;
    }
}
