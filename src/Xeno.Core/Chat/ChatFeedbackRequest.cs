namespace Xeno.Core.Chat
{
    /// <summary>
    /// Rate an assistant message.
    /// </summary>
    public sealed class ChatFeedbackRequest
    {
        /// <summary>
        /// 1 for thumbs up, -1 for thumbs down.
        /// </summary>
        public int Rating { get; set; } = 1;

        /// <summary>
        /// Optional comment.
        /// </summary>
        public string? Comment { get; set; } = null;
    }
}
