namespace Xeno.Client.Tools
{
    using System;

    /// <summary>
    /// Result of the xeno_chat tool.
    /// </summary>
    public sealed class XenoChatToolResult
    {
        /// <summary>
        /// Conversation id; pass it back to continue the conversation.
        /// </summary>
        public string ConversationId { get; set; } = String.Empty;

        /// <summary>
        /// Turn id in chat history.
        /// </summary>
        public string TurnId { get; set; } = String.Empty;

        /// <summary>
        /// Model that answered.
        /// </summary>
        public string Model { get; set; } = String.Empty;

        /// <summary>
        /// Where it ran: "local:&lt;provider&gt;" or "network".
        /// </summary>
        public string Route { get; set; } = String.Empty;

        /// <summary>
        /// Answer text.
        /// </summary>
        public string Answer { get; set; } = String.Empty;

        /// <summary>
        /// Outcome: Success, Error, or Cancelled.
        /// </summary>
        public string Status { get; set; } = String.Empty;

        /// <summary>
        /// Error, or null.
        /// </summary>
        public string? Error { get; set; } = null;

        /// <summary>
        /// Why the model did not answer (NoResponse, Timeout, Unavailable, ModelError, Interrupted, Stopped, Failed), or null.
        /// </summary>
        public string? FailureKind { get; set; } = null;

        /// <summary>
        /// Milliseconds the turn took.
        /// </summary>
        public double TotalMs { get; set; } = 0;
    }
}
