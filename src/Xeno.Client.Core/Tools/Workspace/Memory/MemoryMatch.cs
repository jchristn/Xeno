namespace Xeno.Client.Tools.Workspace.Memory
{
    using System;
    using System.Text.Json.Serialization;

    /// <summary>
    /// One memory in a recall search result, with content clipped to 500 characters.
    /// </summary>
    public sealed class MemoryMatch
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
        /// Content, clipped.
        /// </summary>
        [JsonPropertyName("content")]
        public string Content { get; set; } = String.Empty;
    }
}
