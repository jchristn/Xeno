namespace Xeno.Client.LocalDashboard.Models
{
    /// <summary>
    /// Flattened local model entry.
    /// </summary>
    public class LocalModelEntry
    {
        /// <summary>
        /// Provider name.
        /// </summary>
        public string Provider { get; set; } = string.Empty;

        /// <summary>
        /// Provider API type.
        /// </summary>
        public string ApiType { get; set; } = string.Empty;

        /// <summary>
        /// Endpoint URL.
        /// </summary>
        public string Endpoint { get; set; } = string.Empty;

        /// <summary>
        /// Model name.
        /// </summary>
        public string Name { get; set; } = string.Empty;

        /// <summary>
        /// Supports completions.
        /// </summary>
        public bool SupportsCompletions { get; set; } = true;

        /// <summary>
        /// Supports embeddings.
        /// </summary>
        public bool SupportsEmbeddings { get; set; } = false;

        /// <summary>
        /// Whether the backing Ollama provider allows server-initiated model management.
        /// </summary>
        public bool ModelManagementAllowed { get; set; } = false;

        /// <summary>
        /// Model concurrency limit. Null when the model inherits the provider limit.
        /// </summary>
        public int? MaxConcurrentRequests { get; set; } = null;

        /// <summary>
        /// Concurrency limit the model can actually reach: the smaller of the model and provider limits.
        /// </summary>
        public int EffectiveMaxConcurrentRequests { get; set; } = 4;

        /// <summary>
        /// Whether the provider's models are offered to the Xeno network.
        /// </summary>
        public bool ShareWithNetwork { get; set; } = true;
    }
}
