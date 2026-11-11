namespace Xeno.Core.Chat
{
    using System;

    /// <summary>
    /// Chat activity for one model.
    /// </summary>
    public sealed class ChatModelBreakdown
    {
        /// <summary>
        /// Model.
        /// </summary>
        public string Model { get; set; } = String.Empty;

        /// <summary>
        /// Turns.
        /// </summary>
        public int Turns { get; set; } = 0;

        /// <summary>
        /// Turns that failed or were cancelled.
        /// </summary>
        public int Errors { get; set; } = 0;

        /// <summary>
        /// Median turn time in milliseconds.
        /// </summary>
        public double? P50Ms { get; set; } = null;

        /// <summary>
        /// Thumbs up.
        /// </summary>
        public int ThumbsUp { get; set; } = 0;

        /// <summary>
        /// Thumbs down.
        /// </summary>
        public int ThumbsDown { get; set; } = 0;

        /// <summary>
        /// Completion tokens.
        /// </summary>
        public long CompletionTokens { get; set; } = 0;
    }
}
