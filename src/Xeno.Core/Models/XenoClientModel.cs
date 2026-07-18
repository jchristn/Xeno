namespace Xeno.Core.Models
{
    using System;
    using Xeno.Core.Enums;

    /// <summary>
    /// Model advertised by a connected Xeno client.
    /// </summary>
    public class XenoClientModel
    {
        /// <summary>
        /// Model name.
        /// </summary>
        public string Name { get; set; } = "llama3.2:latest";

        /// <summary>
        /// Upstream API type used by the client.
        /// </summary>
        public ApiProviderTypeEnum Provider { get; set; } = ApiProviderTypeEnum.Ollama;

        /// <summary>
        /// Client-local upstream label or source.
        /// </summary>
        public string Source { get; set; } = "local";

        /// <summary>
        /// Whether completions are supported.
        /// </summary>
        public bool SupportsCompletions { get; set; } = true;

        /// <summary>
        /// Whether embeddings are supported.
        /// </summary>
        public bool SupportsEmbeddings { get; set; } = false;

        /// <summary>
        /// Whether the backing provider allows server-initiated model management operations.
        /// </summary>
        public bool ModelManagementAllowed { get; set; } = false;

        /// <summary>
        /// Last advertisement timestamp.
        /// </summary>
        public DateTime LastAdvertisedUtc { get; set; } = DateTime.UtcNow;
    }
}
