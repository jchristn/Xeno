namespace Xeno.Client.LocalDashboard.Models
{
    /// <summary>
    /// Local provider create or update request.
    /// </summary>
    public class LocalProviderUpsertRequest
    {
        /// <summary>
        /// Provider name.
        /// </summary>
        public string Name { get; set; } = string.Empty;

        /// <summary>
        /// Upstream API type.
        /// </summary>
        public string ApiType { get; set; } = "Ollama";

        /// <summary>
        /// Endpoint URL.
        /// </summary>
        public string Endpoint { get; set; } = string.Empty;

        /// <summary>
        /// Optional credential replacement.
        /// </summary>
        public string? Credential { get; set; } = null;

        /// <summary>
        /// Clear credential.
        /// </summary>
        public bool ClearCredential { get; set; } = false;

        /// <summary>
        /// Timeout in milliseconds.
        /// </summary>
        public int TimeoutMs { get; set; } = 300000;

        /// <summary>
        /// Allow server-initiated model pull and delete operations for Ollama providers.
        /// </summary>
        public bool? AllowModelManagement { get; set; } = null;

        /// <summary>
        /// Whether the provider's models are offered to the Xeno network. Null keeps the current value (true for a new provider).
        /// </summary>
        public bool? ShareWithNetwork { get; set; } = null;

        /// <summary>
        /// Whether the local inference listener may route requests to the provider. Null keeps the current value (true for a new provider).
        /// </summary>
        public bool? RouteLocalRequests { get; set; } = null;

        /// <summary>
        /// Maximum concurrent requests across the provider's models. Null inherits the global default. Minimum 1, maximum 256.
        /// </summary>
        public int? MaxConcurrentRequests { get; set; } = null;
    }
}
