namespace Xeno.Core.Chat
{
    using System;
    using System.Collections.Generic;

    /// <summary>
    /// Chat activity over a time range: totals, latency percentiles, feedback, buckets, and a per-model breakdown.
    /// </summary>
    public sealed class ChatAnalytics
    {
        /// <summary>
        /// Range start, UTC.
        /// </summary>
        public DateTime FromUtc { get; set; } = DateTime.UtcNow;

        /// <summary>
        /// Range end, UTC.
        /// </summary>
        public DateTime ToUtc { get; set; } = DateTime.UtcNow;

        /// <summary>
        /// Bucket size in whole minutes, at least 1 (see <see cref="BucketSeconds"/> for buckets under a minute).
        /// </summary>
        public int BucketMinutes { get; set; } = 60;

        /// <summary>
        /// Bucket size in seconds.
        /// </summary>
        public int BucketSeconds { get; set; } = 3600;

        /// <summary>
        /// Turns.
        /// </summary>
        public int Turns { get; set; } = 0;

        /// <summary>
        /// Turns answered on this machine.
        /// </summary>
        public int Local { get; set; } = 0;

        /// <summary>
        /// Turns answered by the Xeno network.
        /// </summary>
        public int Network { get; set; } = 0;

        /// <summary>
        /// Turns retried on the network after a local failure.
        /// </summary>
        public int Fallbacks { get; set; } = 0;

        /// <summary>
        /// Turns that failed or were cancelled.
        /// </summary>
        public int Errors { get; set; } = 0;

        /// <summary>
        /// Errors divided by turns, 0 to 1.
        /// </summary>
        public double ErrorRate { get; set; } = 0;

        /// <summary>
        /// Median turn time in milliseconds.
        /// </summary>
        public double? P50Ms { get; set; } = null;

        /// <summary>
        /// 95th percentile turn time in milliseconds.
        /// </summary>
        public double? P95Ms { get; set; } = null;

        /// <summary>
        /// Median time to first answer text in milliseconds.
        /// </summary>
        public double? P50TimeToFirstTokenMs { get; set; } = null;

        /// <summary>
        /// Prompt tokens.
        /// </summary>
        public long PromptTokens { get; set; } = 0;

        /// <summary>
        /// Completion tokens.
        /// </summary>
        public long CompletionTokens { get; set; } = 0;

        /// <summary>
        /// Tool calls.
        /// </summary>
        public int ToolCalls { get; set; } = 0;

        /// <summary>
        /// Thumbs up on turns in the range.
        /// </summary>
        public int ThumbsUp { get; set; } = 0;

        /// <summary>
        /// Thumbs down on turns in the range.
        /// </summary>
        public int ThumbsDown { get; set; } = 0;

        /// <summary>
        /// Thumbs down divided by all ratings, 0 to 1, or null with no ratings.
        /// </summary>
        public double? NegativeRate { get; set; } = null;

        /// <summary>
        /// Buckets, oldest first, covering the whole range.
        /// </summary>
        public List<ChatAnalyticsBucket> Buckets { get; set; } = new List<ChatAnalyticsBucket>();

        /// <summary>
        /// Per-model breakdown, busiest first.
        /// </summary>
        public List<ChatModelBreakdown> Models { get; set; } = new List<ChatModelBreakdown>();
    }
}
