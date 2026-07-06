namespace Xeno.Core.Models
{
    using System;
    using System.Text.Json.Serialization;

    /// <summary>
    /// Server-to-client request cancellation notification.
    /// </summary>
    public class ServerRequestCancellationMessage : WebSocketMessageEnvelope
    {
        /// <summary>
        /// Xeno request ID.
        /// </summary>
        [JsonPropertyName("requestId")]
        public string RequestId { get; set; } = String.Empty;

        /// <summary>
        /// Cancellation reason.
        /// </summary>
        [JsonPropertyName("reason")]
        public string Reason { get; set; } = String.Empty;

        /// <summary>
        /// Original caller IP address.
        /// </summary>
        [JsonPropertyName("requestorIp")]
        public string? RequestorIp { get; set; } = null;

        /// <summary>
        /// HTTP method sent upstream.
        /// </summary>
        [JsonPropertyName("method")]
        public string Method { get; set; } = "POST";

        /// <summary>
        /// OpenAI-compatible relative path.
        /// </summary>
        [JsonPropertyName("relativePath")]
        public string RelativePath { get; set; } = "/v1/chat/completions";

        /// <summary>
        /// Client-local provider source name.
        /// </summary>
        [JsonPropertyName("source")]
        public string? Source { get; set; } = null;
    }
}
