namespace Xeno.Client.Tools.Mcp
{
    using System;
    using System.Collections.Generic;
    using System.Text.Json;
    using System.Text.Json.Serialization;

    /// <summary>
    /// The result of tools/call.
    /// </summary>
    internal sealed class McpClientCallResult
    {
        /// <summary>
        /// Content blocks.
        /// </summary>
        [JsonPropertyName("content")]
        public List<McpClientContent>? Content { get; set; } = null;

        /// <summary>
        /// Structured result, used when there are no content blocks.
        /// </summary>
        [JsonPropertyName("structuredContent")]
        public object? StructuredContent { get; set; } = null;

        /// <summary>
        /// True when the tool reports a failure.
        /// </summary>
        [JsonPropertyName("isError")]
        public bool? IsError { get; set; } = null;

        /// <summary>
        /// The result as text: text blocks joined by new lines, other blocks described.
        /// </summary>
        /// <returns>Text.</returns>
        public string ToText()
        {
            List<string> parts = new List<string>();
            if (Content != null)
            {
                foreach (McpClientContent block in Content)
                {
                    if (block == null) continue;
                    string? text = block.ToText();
                    if (text != null) parts.Add(text);
                }
            }

            if (parts.Count == 0 && StructuredContent != null) return JsonSerializer.Serialize(StructuredContent);
            return String.Join("\n", parts);
        }
    }
}
