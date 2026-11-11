namespace Xeno.Core.Chat
{
    using System;

    /// <summary>
    /// Chat activity in one time bucket.
    /// </summary>
    public sealed class ChatAnalyticsBucket
    {
        /// <summary>
        /// Bucket start, UTC.
        /// </summary>
        public DateTime StartUtc { get; set; } = DateTime.UtcNow;

        /// <summary>
        /// Turns answered by a provider on this machine.
        /// </summary>
        public int Local { get; set; } = 0;

        /// <summary>
        /// Turns answered by the Xeno network (including fallbacks).
        /// </summary>
        public int Network { get; set; } = 0;

        /// <summary>
        /// Turns that failed or were cancelled.
        /// </summary>
        public int Errors { get; set; } = 0;

        /// <summary>
        /// Thumbs up given to turns in the bucket.
        /// </summary>
        public int ThumbsUp { get; set; } = 0;

        /// <summary>
        /// Thumbs down given to turns in the bucket.
        /// </summary>
        public int ThumbsDown { get; set; } = 0;

        /// <summary>
        /// Completion tokens.
        /// </summary>
        public long CompletionTokens { get; set; } = 0;
    }
}
