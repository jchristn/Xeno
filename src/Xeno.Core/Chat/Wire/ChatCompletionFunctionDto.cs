namespace Xeno.Core.Chat.Wire
{
    using System;
    using System.Text.Json.Serialization;

    /// <summary>
    /// Function definition offered to the model.
    /// </summary>
    public sealed class ChatCompletionFunctionDto
    {
        /// <summary>
        /// Name.
        /// </summary>
        [JsonPropertyName("name")]
        public string Name { get; set; } = String.Empty;

        /// <summary>
        /// What the function does.
        /// </summary>
        [JsonPropertyName("description")]
        public string Description { get; set; } = String.Empty;

        /// <summary>
        /// JSON schema of the arguments.
        /// </summary>
        [JsonPropertyName("parameters")]
        public ChatToolSchemaDto Parameters { get; set; } = new ChatToolSchemaDto();
    }
}
