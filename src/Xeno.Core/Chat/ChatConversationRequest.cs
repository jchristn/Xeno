namespace Xeno.Core.Chat
{
    /// <summary>
    /// Create or change a conversation. Null members are left unchanged on update.
    /// </summary>
    public sealed class ChatConversationRequest
    {
        /// <summary>
        /// Title.
        /// </summary>
        public string? Title { get; set; } = null;

        /// <summary>
        /// Model.
        /// </summary>
        public string? Model { get; set; } = null;

        /// <summary>
        /// System prompt; an empty string removes it.
        /// </summary>
        public string? SystemPrompt { get; set; } = null;

        /// <summary>
        /// Offer Xeno's tools to the model.
        /// </summary>
        public bool? ToolsEnabled { get; set; } = null;

        /// <summary>
        /// Working directory for file and process tools; null leaves it unchanged, an empty string clears it.
        /// </summary>
        public string? WorkingDirectory { get; set; } = null;

        /// <summary>
        /// Keep at the top of lists.
        /// </summary>
        public bool? Pinned { get; set; } = null;

        /// <summary>
        /// Surface creating the conversation.
        /// </summary>
        public ChatSurfaceEnum? Surface { get; set; } = null;
    }
}
