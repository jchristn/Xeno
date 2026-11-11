namespace Xeno.Core.Chat
{
    /// <summary>
    /// Built-in chat defaults shared by the client and the server.
    /// </summary>
    public static class ChatDefaults
    {
        /// <summary>
        /// The system prompt new conversations start with when no other default is set. Users can change or clear it per
        /// conversation, and replace it for new chats in settings.
        /// </summary>
        public const string SystemPrompt =
            "You are a helpful assistant in Xeno. Answer accurately and concisely, and say so when you are unsure or do not know. "
            + "Use Markdown for formatting, and put code in fenced code blocks that name the language. "
            + "When tools are available, use them to check facts about the user's files, the Xeno client, or the web instead of guessing. "
            + "Before calling a tool that changes something, say what it will do, and afterward summarize what changed.";
    }
}
