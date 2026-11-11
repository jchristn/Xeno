namespace Xeno.Core.Chat
{
    using System;
    using System.Collections.Generic;

    /// <summary>
    /// A saved chat conversation.
    /// </summary>
    public sealed class ChatConversation
    {
        /// <summary>
        /// Identifier, prefixed "cnv_".
        /// </summary>
        public string Id { get; set; } = String.Empty;

        /// <summary>
        /// Title shown in conversation lists.
        /// </summary>
        public string Title { get; set; } = "New chat";

        /// <summary>
        /// Model the next turn is sent to.
        /// </summary>
        public string Model { get; set; } = String.Empty;

        /// <summary>
        /// System prompt sent before the conversation, or null for none.
        /// </summary>
        public string? SystemPrompt { get; set; } = null;

        /// <summary>
        /// True to offer Xeno's tools to the model.
        /// </summary>
        public bool ToolsEnabled { get; set; } = false;

        /// <summary>
        /// Absolute directory that file and process tools work in, or null for the client's default (the user's home
        /// folder). Tools cannot reach outside it.
        /// </summary>
        public string? WorkingDirectory { get; set; } = null;

        /// <summary>
        /// True to keep the conversation at the top of lists.
        /// </summary>
        public bool Pinned { get; set; } = false;

        /// <summary>
        /// Surface the conversation was started from.
        /// </summary>
        public ChatSurfaceEnum Surface { get; set; } = ChatSurfaceEnum.Web;

        /// <summary>
        /// Number of messages, filled in by lists.
        /// </summary>
        public int MessageCount { get; set; } = 0;

        /// <summary>
        /// Created, UTC.
        /// </summary>
        public DateTime CreatedUtc { get; set; } = DateTime.UtcNow;

        /// <summary>
        /// Last changed, UTC.
        /// </summary>
        public DateTime UpdatedUtc { get; set; } = DateTime.UtcNow;

        /// <summary>
        /// Messages in order. Filled in only when a single conversation is read.
        /// </summary>
        public List<ChatMessage> Messages { get; set; } = new List<ChatMessage>();
    }
}
