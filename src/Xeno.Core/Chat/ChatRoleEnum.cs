namespace Xeno.Core.Chat
{
    /// <summary>
    /// Role of a chat message.
    /// </summary>
    public enum ChatRoleEnum
    {
        /// <summary>
        /// System prompt.
        /// </summary>
        System,

        /// <summary>
        /// Message from the user.
        /// </summary>
        User,

        /// <summary>
        /// Answer from the model.
        /// </summary>
        Assistant,

        /// <summary>
        /// Result of a tool call, sent back to the model.
        /// </summary>
        Tool
    }
}
