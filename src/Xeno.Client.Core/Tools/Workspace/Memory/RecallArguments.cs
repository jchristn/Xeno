namespace Xeno.Client.Tools.Workspace.Memory
{
    using System.Text.Json.Serialization;

    /// <summary>
    /// Arguments of recall.
    /// </summary>
    public sealed class RecallArguments
    {
        /// <summary>
        /// A memory name to read in full.
        /// </summary>
        [JsonPropertyName("name")]
        public string? Name { get; set; } = null;

        /// <summary>
        /// Words to search for in names, descriptions, and content.
        /// </summary>
        [JsonPropertyName("query")]
        public string? Query { get; set; } = null;
    }
}
