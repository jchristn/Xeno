namespace Xeno.Core.Models
{
    using System;
    using System.Collections.Generic;
    using System.Text.Json.Serialization;

    /// <summary>
    /// Server-to-client local inference proxy response.
    /// </summary>
    public class ServerLocalInferenceResponseMessage : WebSocketMessageEnvelope
    {
        /// <summary>
        /// Request ID the response belongs to.
        /// </summary>
        [JsonPropertyName("requestId")]
        public string RequestId { get; set; } = String.Empty;

        /// <summary>
        /// Whether the server completed the exchange successfully.
        /// </summary>
        [JsonPropertyName("success")]
        public bool Success { get; set; } = false;

        /// <summary>
        /// HTTP status code to return to the localhost caller.
        /// </summary>
        [JsonPropertyName("statusCode")]
        public int? StatusCode { get; set; } = null;

        /// <summary>
        /// Response content type.
        /// </summary>
        [JsonPropertyName("contentType")]
        public string? ContentType { get; set; } = null;

        /// <summary>
        /// Response headers returned by Xeno Server or the selected upstream provider.
        /// </summary>
        [JsonPropertyName("headers")]
        public Dictionary<string, string> Headers { get; set; } = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        /// <summary>
        /// Buffered response body or final captured streaming body.
        /// </summary>
        [JsonPropertyName("body")]
        public string? Body { get; set; } = null;

        /// <summary>
        /// Error message when the request failed.
        /// </summary>
        [JsonPropertyName("error")]
        public string? Error { get; set; } = null;
    }
}
