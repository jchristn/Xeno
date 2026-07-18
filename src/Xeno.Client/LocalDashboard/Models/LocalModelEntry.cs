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
    }
}
