namespace Xeno.Core.Chat
{
    using System;

    /// <summary>
    /// Where a tool call runs: the conversation and turn it belongs to and the conversation's working directory, which
    /// file and process tools are confined to.
    /// </summary>
    public sealed class ChatToolContext
    {
        /// <summary>
        /// Conversation id.
        /// </summary>
        public string ConversationId { get; set; } = String.Empty;

        /// <summary>
        /// Turn id.
        /// </summary>
        public string TurnId { get; set; } = String.Empty;

        /// <summary>
        /// Absolute working directory for file and process tools, or null when the conversation has none (the host
        /// then uses its default).
        /// </summary>
        public string? WorkingDirectory { get; set; } = null;
    }
}
