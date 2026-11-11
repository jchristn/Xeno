namespace Xeno.Core.Chat
{
    using System;
    using System.Collections.Generic;

    /// <summary>
    /// Models offered for chat.
    /// </summary>
    public sealed class ChatModelList
    {
        /// <summary>Models.</summary>
        public List<ChatModelOption> Entries { get; set; } = new List<ChatModelOption>();

        /// <summary>Why network models are missing, or empty.</summary>
        public string NetworkMessage { get; set; } = String.Empty;
    }
}
