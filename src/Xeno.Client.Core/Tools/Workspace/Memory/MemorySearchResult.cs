namespace Xeno.Client.Tools.Workspace.Memory
{
    using System.Collections.Generic;
    using System.Text.Json.Serialization;

    /// <summary>
    /// Result of recall without a name: matching memories, project first.
    /// </summary>
    public sealed class MemorySearchResult
    {
        /// <summary>
        /// Number of matches.
        /// </summary>
        [JsonPropertyName("count")]
        public int Count { get; set; } = 0;

        /// <summary>
        /// Matches.
        /// </summary>
        [JsonPropertyName("memories")]
        public List<MemoryMatch> Memories { get; set; } = new List<MemoryMatch>();
    }
}
