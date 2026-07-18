namespace Xeno.Core.Models
{
    using System;
    using System.Text.Json.Serialization;

    /// <summary>
    /// Server-to-client model management request.
    /// </summary>
    public class ServerModelManagementRequestMessage : WebSocketMessageEnvelope
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
        /// Target client ID.
        /// </summary>
        [JsonPropertyName("clientId")]
        public string ClientId { get; set; } = String.Empty;

        /// <summary>
        /// Operation to execute, pull or delete.
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
        /// Whether Ollama may download over insecure connections.
        /// </summary>
        [JsonPropertyName("insecure")]
        public bool Insecure { get; set; } = false;

        /// <summary>
        /// Request timeout in milliseconds.
        /// </summary>
        [JsonPropertyName("timeoutMs")]
        public int TimeoutMs { get; set; } = 300000;
    }
}
