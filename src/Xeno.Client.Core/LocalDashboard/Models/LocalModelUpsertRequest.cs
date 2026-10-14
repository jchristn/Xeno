namespace Xeno.Client.LocalDashboard.Models
{
    /// <summary>
    /// Local model create or update request.
    /// </summary>
    public class LocalModelUpsertRequest
    {
        /// <summary>
        /// Provider name.
        /// </summary>
        public string Provider { get; set; } = string.Empty;

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
    }
}
