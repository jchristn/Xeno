namespace Xeno.Client.Tools.Workspace.Memory
{
    using System;
    using System.Text.Json.Serialization;

    /// <summary>
    /// Result of remember.
    /// </summary>
    public sealed class MemorySavedResult
    {
        /// <summary>
        /// Always true.
        /// </summary>
        [JsonPropertyName("saved")]
        public bool Saved { get; set; } = true;

        /// <summary>
        /// True when a new memory was created, false when an existing one was updated.
        /// </summary>
        [JsonPropertyName("created")]
        public bool Created { get; set; } = false;

        /// <summary>
        /// Slug of the memory.
        /// </summary>
        [JsonPropertyName("name")]
        public string Name { get; set; } = String.Empty;

        /// <summary>
        /// project or global.
        /// </summary>
        [JsonPropertyName("scope")]
        public string Scope { get; set; } = String.Empty;

        /// <summary>
        /// Saved one-line description.
        /// </summary>
        [JsonPropertyName("description")]
        public string Description { get; set; } = String.Empty;
    }
}
