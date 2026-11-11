namespace Xeno.Client.Tools.Mcp
{
    using System.Text.Json.Serialization;

    /// <summary>
    /// Hints an MCP server gives about one tool.
    /// </summary>
    internal sealed class McpClientToolAnnotations
    {
        /// <summary>
        /// True when the tool does not change anything.
        /// </summary>
        [JsonPropertyName("readOnlyHint")]
        public bool? ReadOnlyHint { get; set; } = null;
    }
}
