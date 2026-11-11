namespace Xeno.Core.Chat
{
    using System;

    /// <summary>
    /// A user message for a conversation.
    /// </summary>
    public sealed class ChatSendRequest
    {
        /// <summary>
        /// Message text. Required.
        /// </summary>
        public string Content { get; set; } = String.Empty;

        /// <summary>
        /// Model for this turn, or null to use the conversation's model. A given model also becomes the
        /// conversation's model.
        /// </summary>
        public string? Model { get; set; } = null;

        /// <summary>
        /// True (the default) to stream the answer from the model as it is written; false to wait for the whole answer.
        /// </summary>
        public bool Stream { get; set; } = true;

        /// <summary>
        /// Surface sending the message.
        /// </summary>
        public ChatSurfaceEnum Surface { get; set; } = ChatSurfaceEnum.Web;
    }
}
