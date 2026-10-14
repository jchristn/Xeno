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
    }
}
