namespace Xeno.Client.Tools.Mcp
{
    using System.Text.Json.Serialization;

    /// <summary>
    /// One tool from an MCP server's tools/list.
    /// </summary>
    internal sealed class McpClientToolDefinition
    {
        /// <summary>
        /// Name on the server.
        /// </summary>
        [JsonPropertyName("name")]
        public string? Name { get; set; } = null;

        /// <summary>
        /// Display title.
        /// </summary>
        [JsonPropertyName("title")]
        public string? Title { get; set; } = null;

        /// <summary>
        /// Description.
        /// </summary>
        [JsonPropertyName("description")]
        public string? Description { get; set; } = null;

        /// <summary>
        /// Input schema, kept as received and converted separately so one odd schema never drops the server's tool list.
        /// </summary>
        [JsonPropertyName("inputSchema")]
        public object? InputSchema { get; set; } = null;

        /// <summary>
        /// Hints, or null.
        /// </summary>
        [JsonPropertyName("annotations")]
        public McpClientToolAnnotations? Annotations { get; set; } = null;
    }
}
