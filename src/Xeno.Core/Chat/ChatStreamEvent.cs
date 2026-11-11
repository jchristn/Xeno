namespace Xeno.Core.Chat
{
    /// <summary>
    /// One event streamed while a chat turn runs.
    /// </summary>
    public sealed class ChatStreamEvent
    {
        /// <summary>
        /// Kind.
        /// </summary>
        public ChatStreamEventKindEnum Kind { get; set; } = ChatStreamEventKindEnum.Chunk;

        /// <summary>
        /// Conversation identifier.
        /// </summary>
        public string? ConversationId { get; set; } = null;

        /// <summary>
        /// Turn identifier.
        /// </summary>
        public string? TurnId { get; set; } = null;

        /// <summary>
        /// For Turn, the user message; for Usage, the assistant message.
        /// </summary>
        public string? MessageId { get; set; } = null;

        /// <summary>
        /// Text for Chunk, Thinking, and Error.
        /// </summary>
        public string? Text { get; set; } = null;

        /// <summary>
        /// Tool call for ToolCall.
        /// </summary>
        public ChatToolCallRecord? ToolCall { get; set; } = null;

        /// <summary>
        /// The turn record for Usage and Done.
        /// </summary>
        public ChatTurn? Turn { get; set; } = null;

        /// <summary>
        /// For an error event: why the model did not answer.
        /// </summary>
        public ChatFailure? Failure { get; set; } = null;
    }
}
