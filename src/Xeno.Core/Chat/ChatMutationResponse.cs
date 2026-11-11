namespace Xeno.Core.Chat
{
    /// <summary>
    /// Result of a chat change.
    /// </summary>
    public sealed class ChatMutationResponse
    {
        /// <summary>
        /// True when something changed.
        /// </summary>
        public bool Changed { get; set; } = false;

        /// <summary>
        /// Number of records removed, for deletes.
        /// </summary>
        public int Removed { get; set; } = 0;
    }
}
