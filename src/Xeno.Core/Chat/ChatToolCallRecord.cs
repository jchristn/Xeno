namespace Xeno.Core.Chat
{
    using System;

    /// <summary>
    /// A tool call the model made during a turn.
    /// </summary>
    public sealed class ChatToolCallRecord
    {
        /// <summary>
        /// Identifier, prefixed "tcl_".
        /// </summary>
        public string Id { get; set; } = String.Empty;

        /// <summary>
        /// Identifier the model gave the call; the tool's answer refers to it.
        /// </summary>
        public string CallId { get; set; } = String.Empty;

        /// <summary>
        /// Turn identifier.
        /// </summary>
        public string TurnId { get; set; } = String.Empty;

        /// <summary>
        /// Assistant message that asked for the call.
        /// </summary>
        public string MessageId { get; set; } = String.Empty;

        /// <summary>
        /// Tool name.
        /// </summary>
        public string ToolName { get; set; } = String.Empty;

        /// <summary>
        /// Arguments as the model sent them (JSON text).
        /// </summary>
        public string Arguments { get; set; } = "{}";

        /// <summary>
        /// Result sent back to the model (JSON text, truncated), or null when the call did not run.
        /// </summary>
        public string? Result { get; set; } = null;

        /// <summary>
        /// True when the tool changes something, so it asks before running.
        /// </summary>
        public bool Mutating { get; set; } = false;

        /// <summary>
        /// Approval.
        /// </summary>
        public ChatToolApprovalEnum Approval { get; set; } = ChatToolApprovalEnum.Auto;

        /// <summary>
        /// True when the tool ran and succeeded.
        /// </summary>
        public bool Success { get; set; } = false;

        /// <summary>
        /// Error, or null.
        /// </summary>
        public string? Error { get; set; } = null;

        /// <summary>
        /// Milliseconds the tool took.
        /// </summary>
        public double DurationMs { get; set; } = 0;

        /// <summary>
        /// Milliseconds the call waited for Allow or Deny (0 for tools that run without asking).
        /// </summary>
        public double ApprovalMs { get; set; } = 0;

        /// <summary>
        /// Created, UTC.
        /// </summary>
        public DateTime CreatedUtc { get; set; } = DateTime.UtcNow;
    }
}
