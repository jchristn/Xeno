namespace Xeno.Client.Tools
{
    /// <summary>
    /// Arguments of the xeno_chat tool.
    /// </summary>
    public sealed class XenoChatToolArguments
    {
        /// <summary>
        /// Model to ask. Required for a new conversation.
        /// </summary>
        public string? Model { get; set; } = null;

        /// <summary>
        /// Message. Required.
        /// </summary>
        public string? Message { get; set; } = null;

        /// <summary>
        /// Conversation to continue, or null to start one.
        /// </summary>
        public string? ConversationId { get; set; } = null;

        /// <summary>
        /// System prompt for a new conversation.
        /// </summary>
        public string? SystemPrompt { get; set; } = null;
    }
}
