namespace Xeno.Core.Chat.Wire
{
    using System;
    using System.Collections.Generic;
    using System.Text.Json.Serialization;

    /// <summary>
    /// One property of a tool's argument schema.
    /// </summary>
    public sealed class ChatToolPropertyDto
    {
        /// <summary>
        /// "string", "integer", "number", "boolean", "array", or "object".
        /// </summary>
        [JsonPropertyName("type")]
        public string Type { get; set; } = "string";

        /// <summary>
        /// What the property means.
        /// </summary>
        [JsonPropertyName("description")]
        public string Description { get; set; } = String.Empty;

        /// <summary>
        /// Allowed values, or null for any.
        /// </summary>
        [JsonPropertyName("enum")]
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public List<string>? Enum { get; set; } = null;

        /// <summary>
        /// Schema of each element when <see cref="Type"/> is "array", or null.
        /// </summary>
        [JsonPropertyName("items")]
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public ChatToolPropertyDto? Items { get; set; } = null;

        /// <summary>
        /// Properties when <see cref="Type"/> is "object", or null.
        /// </summary>
        [JsonPropertyName("properties")]
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public Dictionary<string, ChatToolPropertyDto>? Properties { get; set; } = null;

        /// <summary>
        /// Required properties when <see cref="Type"/> is "object", or null.
        /// </summary>
        [JsonPropertyName("required")]
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public List<string>? Required { get; set; } = null;
    }
}
