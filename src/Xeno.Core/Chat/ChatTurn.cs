namespace Xeno.Core.Chat
{
    using System;
    using System.Collections.Generic;

    /// <summary>
    /// Request history record for one chat turn: what was asked of which model, where it went, and how it went.
    /// Holds no prompt or answer text; that stays in the conversation.
    /// </summary>
    public sealed class ChatTurn
    {
        /// <summary>
        /// Identifier, prefixed "trn_".
        /// </summary>
        public string Id { get; set; } = String.Empty;

        /// <summary>
        /// Conversation identifier.
        /// </summary>
        public string ConversationId { get; set; } = String.Empty;

        /// <summary>
        /// Conversation title, filled in by queries.
        /// </summary>
        public string? ConversationTitle { get; set; } = null;

        /// <summary>
        /// User message that started the turn.
        /// </summary>
        public string? UserMessageId { get; set; } = null;

        /// <summary>
        /// Final assistant message, or null when the turn failed before answering.
        /// </summary>
        public string? AssistantMessageId { get; set; } = null;

        /// <summary>
        /// Model.
        /// </summary>
        public string Model { get; set; } = String.Empty;

        /// <summary>
        /// Route: "local:&lt;provider&gt;", "network", or "unavailable".
        /// </summary>
        public string Route { get; set; } = String.Empty;

        /// <summary>
        /// Local provider name, or empty for the network.
        /// </summary>
        public string Provider { get; set; } = String.Empty;

        /// <summary>
        /// True when a local provider failed and the turn was retried on the network.
        /// </summary>
        public bool Fallback { get; set; } = false;

        /// <summary>
        /// Status.
        /// </summary>
        public ChatTurnStatusEnum Status { get; set; } = ChatTurnStatusEnum.Running;

        /// <summary>
        /// Error, or null.
        /// </summary>
        public string? Error { get; set; } = null;

        /// <summary>
        /// HTTP status the model call returned, or null.
        /// </summary>
        public int? StatusCode { get; set; } = null;

        /// <summary>
        /// Prompt tokens, summed over every model call in the turn.
        /// </summary>
        public int? PromptTokens { get; set; } = null;

        /// <summary>
        /// Completion tokens, summed over every model call in the turn.
        /// </summary>
        public int? CompletionTokens { get; set; } = null;

        /// <summary>
        /// Milliseconds until the first answer text.
        /// </summary>
        public double? TimeToFirstTokenMs { get; set; } = null;

        /// <summary>
        /// Milliseconds for the whole turn.
        /// </summary>
        public double TotalMs { get; set; } = 0;

        /// <summary>
        /// Model calls made (1 without tools).
        /// </summary>
        public int ModelCalls { get; set; } = 0;

        /// <summary>
        /// Tool calls made.
        /// </summary>
        public int ToolCallCount { get; set; } = 0;

        /// <summary>
        /// Milliseconds spent in tools.
        /// </summary>
        public double ToolMs { get; set; } = 0;

        /// <summary>
        /// Milliseconds spent in model calls (all iterations).
        /// </summary>
        public double ModelMs { get; set; } = 0;

        /// <summary>
        /// Milliseconds from the first to the last answer text, summed over model calls; null when nothing streamed.
        /// </summary>
        public double? StreamingMs { get; set; } = null;

        /// <summary>
        /// Milliseconds tool calls waited for Allow or Deny.
        /// </summary>
        public double ApprovalMs { get; set; } = 0;

        /// <summary>
        /// Cached prompt tokens, when the provider reported them.
        /// </summary>
        public int? CachedPromptTokens { get; set; } = null;

        /// <summary>
        /// Tool calls that ran and failed.
        /// </summary>
        public int ToolFailures { get; set; } = 0;

        /// <summary>
        /// Tool calls that were denied, timed out waiting for approval, or stopped before approval.
        /// </summary>
        public int ToolDenied { get; set; } = 0;

        /// <summary>
        /// Surface the turn came from.
        /// </summary>
        public ChatSurfaceEnum Surface { get; set; } = ChatSurfaceEnum.Web;

        /// <summary>
        /// Rating of the answer (1, -1), or null when not rated. Filled in by queries.
        /// </summary>
        public int? FeedbackRating { get; set; } = null;

        /// <summary>
        /// Started, UTC.
        /// </summary>
        public DateTime CreatedUtc { get; set; } = DateTime.UtcNow;

        /// <summary>
        /// Finished, UTC, or null while running.
        /// </summary>
        public DateTime? CompletedUtc { get; set; } = null;

        /// <summary>
        /// Tool calls of the turn. Filled in only when a single turn is read.
        /// </summary>
        public List<ChatToolCallRecord> ToolCalls { get; set; } = new List<ChatToolCallRecord>();
    }
}
