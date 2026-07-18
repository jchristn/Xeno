namespace Xeno.Core.Models
{
    using System;
    using System.Text.Json.Serialization;

    /// <summary>
    /// Client-to-server model management completion response.
    /// </summary>
    public class ClientModelManagementResponseMessage : WebSocketMessageEnvelope
    {
        /// <summary>
        /// Xeno request ID.
        /// </summary>
        [JsonPropertyName("requestId")]
        public string RequestId { get; set; } = String.Empty;

        /// <summary>
        /// Tenant ID.
        /// </summary>
        [JsonPropertyName("tenantId")]
        public string TenantId { get; set; } = String.Empty;

        /// <summary>
        /// Client ID.
        /// </summary>
        [JsonPropertyName("clientId")]
        public string ClientId { get; set; } = String.Empty;

        /// <summary>
        /// Operation that completed.
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
        /// Whether the operation succeeded.
        /// </summary>
        [JsonPropertyName("success")]
        public bool Success { get; set; } = false;

        /// <summary>
        /// Upstream HTTP status code.
        /// </summary>
        [JsonPropertyName("statusCode")]
        public int? StatusCode { get; set; } = null;

        /// <summary>
        /// Response or failure message.
        /// </summary>
        [JsonPropertyName("message")]
        public string Message { get; set; } = String.Empty;

        /// <summary>
        /// Error text when the operation failed.
        /// </summary>
        [JsonPropertyName("error")]
        public string? Error { get; set; } = null;

        /// <summary>
        /// Upstream response body.
        /// </summary>
        [JsonPropertyName("body")]
        public string? Body { get; set; } = null;

        /// <summary>
        /// UTC timestamp when emitted.
        /// </summary>
        [JsonPropertyName("sentUtc")]
        public DateTime SentUtc { get; set; } = DateTime.UtcNow;
    }
}
