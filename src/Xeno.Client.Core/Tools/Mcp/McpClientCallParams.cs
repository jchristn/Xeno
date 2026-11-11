namespace Xeno.Client.Tools.Mcp
{
    using System;
    using System.Collections.Generic;
    using System.Text.Json.Serialization;

    /// <summary>
    /// Parameters of tools/call.
    /// </summary>
    internal sealed class McpClientCallParams
    {
        /// <summary>
        /// Tool name on the server.
        /// </summary>
        [JsonPropertyName("name")]
        public string Name { get; set; } = String.Empty;

        /// <summary>
        /// Arguments, passed through as the model wrote them.
        /// </summary>
        [JsonPropertyName("arguments")]
        public Dictionary<string, object?> Arguments { get; set; } = new Dictionary<string, object?>(StringComparer.Ordinal);
    }
}
