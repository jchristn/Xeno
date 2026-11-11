namespace Xeno.Core.Chat
{
    using System;

    /// <summary>
    /// Ask for a model to be loaded before chatting with it.
    /// </summary>
    public sealed class ChatModelPrepareRequest
    {
        /// <summary>
        /// Model name.
        /// </summary>
        public string Model { get; set; } = String.Empty;
    }
}
