namespace Xeno.Core.Chat.Wire
{
    using System.Collections.Generic;
    using System.Text.Json.Serialization;

    /// <summary>
    /// JSON schema for tool arguments: an object with named properties.
    /// </summary>
    public sealed class ChatToolSchemaDto
    {
        /// <summary>
        /// Always "object" at the top level.
        /// </summary>
        [JsonPropertyName("type")]
        public string Type { get; set; } = "object";

        /// <summary>
        /// Properties by name.
        /// </summary>
        [JsonPropertyName("properties")]
        public Dictionary<string, ChatToolPropertyDto> Properties { get; set; } = new Dictionary<string, ChatToolPropertyDto>();

        /// <summary>
        /// Required property names.
        /// </summary>
        [JsonPropertyName("required")]
        public List<string> Required { get; set; } = new List<string>();
    }
}
