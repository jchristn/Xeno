namespace Xeno.Client.Tools.Workspace.Memory
{
    using System;
    using System.Text.Json.Serialization;

    /// <summary>
    /// Result of recall with a name: one memory in full.
    /// </summary>
    public sealed class MemoryReadResult
    {
        /// <summary>
        /// Slug.
        /// </summary>
        [JsonPropertyName("name")]
        public string Name { get; set; } = String.Empty;

        /// <summary>
        /// project or global.
        /// </summary>
        [JsonPropertyName("scope")]
        public string Scope { get; set; } = String.Empty;

        /// <summary>
        /// One-line description.
        /// </summary>
        [JsonPropertyName("description")]
        public string Description { get; set; } = String.Empty;

        /// <summary>
        /// Full content.
        /// </summary>
        [JsonPropertyName("content")]
        public string Content { get; set; } = String.Empty;

        /// <summary>
        /// Last change, ISO 8601 UTC.
        /// </summary>
        [JsonPropertyName("updated_utc")]
        public string UpdatedUtc { get; set; } = String.Empty;
    }
}
