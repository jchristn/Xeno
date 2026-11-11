namespace Xeno.Core.Chat
{
    using System;
    using System.Collections.Generic;

    /// <summary>
    /// One message in a conversation.
    /// </summary>
    public sealed class ChatMessage
    {
        /// <summary>
        /// Identifier, prefixed "msg_".
        /// </summary>
        public string Id { get; set; } = String.Empty;

        /// <summary>
        /// Conversation identifier.
        /// </summary>
        public string ConversationId { get; set; } = String.Empty;

        /// <summary>
        /// Position in the conversation, starting at 1.
        /// </summary>
        public int Sequence { get; set; } = 0;

        /// <summary>
        /// Role.
        /// </summary>
        public ChatRoleEnum Role { get; set; } = ChatRoleEnum.User;

        /// <summary>
        /// Text. Never null.
        /// </summary>
        public string Content { get; set; } = String.Empty;

        /// <summary>
        /// Reasoning text the model sent separately from its answer, or null.
        /// </summary>
        public string? Thinking { get; set; } = null;

        /// <summary>
        /// Model that wrote an assistant message.
        /// </summary>
        public string? Model { get; set; } = null;

        /// <summary>
        /// Where the turn went: "local:&lt;provider&gt;" or "network".
        /// </summary>
        public string? Route { get; set; } = null;

        /// <summary>
        /// True when a local provider failed and the turn was retried on the network.
        /// </summary>
        public bool Fallback { get; set; } = false;

        /// <summary>
        /// Prompt tokens reported by the provider.
        /// </summary>
        public int? PromptTokens { get; set; } = null;

        /// <summary>
        /// Completion tokens reported by the provider.
        /// </summary>
        public int? CompletionTokens { get; set; } = null;

        /// <summary>
        /// Milliseconds until the first answer text.
        /// </summary>
        public double? TimeToFirstTokenMs { get; set; } = null;

        /// <summary>
        /// Milliseconds the model call took.
        /// </summary>
        public double? TotalMs { get; set; } = null;

        /// <summary>
        /// True when the answer stopped early (an error or a cancel).
        /// </summary>
        public bool Incomplete { get; set; } = false;

        /// <summary>
        /// Why the answer is missing or incomplete, or null when the model answered. An answer with a failure and no
        /// content stands in for the answer the model did not give; it is never sent back to the model.
        /// </summary>
        public ChatFailure? Failure { get; set; } = null;

        /// <summary>
        /// Turn that wrote the message.
        /// </summary>
        public string? TurnId { get; set; } = null;

        /// <summary>
        /// For a Tool message, the tool call it answers.
        /// </summary>
        public string? ToolCallId { get; set; } = null;

        /// <summary>
        /// For an assistant message, the tool calls the model asked for.
        /// </summary>
        public List<ChatToolCallRecord> ToolCalls { get; set; } = new List<ChatToolCallRecord>();

        /// <summary>
        /// Telemetry for an assistant message, or null.
        /// </summary>
        public ChatMessageStats? Stats { get; set; } = null;

        /// <summary>
        /// Feedback on an assistant message, or null.
        /// </summary>
        public ChatFeedback? Feedback { get; set; } = null;

        /// <summary>
        /// Created, UTC.
        /// </summary>
        public DateTime CreatedUtc { get; set; } = DateTime.UtcNow;
    }
}
