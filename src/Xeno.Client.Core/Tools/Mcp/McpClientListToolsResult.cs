namespace Xeno.Client.Tools.Mcp
{
    using System.Collections.Generic;
    using System.Text.Json.Serialization;

    /// <summary>
    /// One page of tools/list.
    /// </summary>
    internal sealed class McpClientListToolsResult
    {
        /// <summary>
        /// Tools on this page.
        /// </summary>
        [JsonPropertyName("tools")]
        public List<McpClientToolDefinition>? Tools { get; set; } = null;

        /// <summary>
        /// Cursor for the next page, or null on the last page.
        /// </summary>
        [JsonPropertyName("nextCursor")]
        public string? NextCursor { get; set; } = null;
    }
}
