namespace Xeno.Client.Tools
{
    using System.Collections.Generic;

    /// <summary>
    /// Saved conversations.
    /// </summary>
    public sealed class XenoToolConversationList
    {
        /// <summary>
        /// Items.
        /// </summary>
        public List<Xeno.Core.Chat.ChatConversation> Conversations { get; set; } = new List<Xeno.Core.Chat.ChatConversation>();
    }
}
