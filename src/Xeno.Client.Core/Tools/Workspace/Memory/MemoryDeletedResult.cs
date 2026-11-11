namespace Xeno.Client.Tools.Workspace.Memory
{
    using System;
    using System.Text.Json.Serialization;

    /// <summary>
    /// Result of forget.
    /// </summary>
    public sealed class MemoryDeletedResult
    {
        /// <summary>
        /// Always true.
        /// </summary>
        [JsonPropertyName("deleted")]
        public bool Deleted { get; set; } = true;

        /// <summary>
        /// Slug of the deleted memory.
        /// </summary>
        [JsonPropertyName("name")]
        public string Name { get; set; } = String.Empty;

        /// <summary>
        /// project or global.
        /// </summary>
        [JsonPropertyName("scope")]
        public string Scope { get; set; } = String.Empty;
    }
}
