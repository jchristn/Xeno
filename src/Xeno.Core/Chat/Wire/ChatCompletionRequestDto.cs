namespace Xeno.Core.Chat.Wire
{
    using System;
    using System.Collections.Generic;
    using System.Text.Json.Serialization;

    /// <summary>
    /// OpenAI-compatible /v1/chat/completions request body.
    /// </summary>
    public sealed class ChatCompletionRequestDto
    {
        /// <summary>
        /// Model.
        /// </summary>
        [JsonPropertyName("model")]
        public string Model { get; set; } = String.Empty;

        /// <summary>
        /// Messages.
        /// </summary>
        [JsonPropertyName("messages")]
        public List<ChatCompletionMessageDto> Messages { get; set; } = new List<ChatCompletionMessageDto>();

        /// <summary>
        /// True to stream server-sent events.
        /// </summary>
        [JsonPropertyName("stream")]
        public bool Stream { get; set; } = true;

        /// <summary>
        /// Stream options; asks for a final usage chunk.
        /// </summary>
        [JsonPropertyName("stream_options")]
        public ChatCompletionStreamOptionsDto? StreamOptions { get; set; } = null;

        /// <summary>
        /// Tools offered to the model, or null for none.
        /// </summary>
        [JsonPropertyName("tools")]
        public List<ChatCompletionToolDto>? Tools { get; set; } = null;
    }
}
