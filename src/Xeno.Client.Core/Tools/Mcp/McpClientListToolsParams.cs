namespace Xeno.Client.Tools.Mcp
{
    using System;
    using System.Text.Json.Serialization;

    /// <summary>
    /// Parameters of tools/list after the first page.
    /// </summary>
    internal sealed class McpClientListToolsParams
    {
        /// <summary>
        /// Cursor from the previous page.
        /// </summary>
        [JsonPropertyName("cursor")]
        public string Cursor { get; set; } = String.Empty;
    }
}
