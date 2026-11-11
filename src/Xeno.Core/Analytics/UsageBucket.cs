namespace Xeno.Core.Analytics
{
    using System;
    using System.Collections.Generic;

    /// <summary>
    /// One time bucket of usage analytics.
    /// </summary>
    public sealed class UsageBucket
    {
        /// <summary>
        /// Start, UTC.
        /// </summary>
        public DateTime BucketStartUtc { get; set; } = DateTime.MinValue;

        /// <summary>
        /// End, UTC.
        /// </summary>
        public DateTime BucketEndUtc { get; set; } = DateTime.MinValue;

        /// <summary>
        /// Requests.
        /// </summary>
        public long Requests { get; set; } = 0;

        /// <summary>
        /// Succeeded.
        /// </summary>
        public long Succeeded { get; set; } = 0;

        /// <summary>
        /// Failed.
        /// </summary>
        public long Failed { get; set; } = 0;

        /// <summary>
        /// Answered by a network fallback; null when the dataset cannot know.
        /// </summary>
        public long? Fallbacks { get; set; } = null;

        /// <summary>
        /// Total milliseconds.
        /// </summary>
        public UsageDistribution Latency { get; set; } = new UsageDistribution();

        /// <summary>
        /// Milliseconds to the first token.
        /// </summary>
        public UsageDistribution TimeToFirstToken { get; set; } = new UsageDistribution();

        /// <summary>
        /// Streaming milliseconds.
        /// </summary>
        public UsageDistribution Streaming { get; set; } = new UsageDistribution();

        /// <summary>
        /// Completion tokens per second.
        /// </summary>
        public UsageDistribution Throughput { get; set; } = new UsageDistribution();

        /// <summary>
        /// Prompt tokens not known to be cached.
        /// </summary>
        public long PromptTokens { get; set; } = 0;

        /// <summary>
        /// Cached prompt tokens; null when none were recorded.
        /// </summary>
        public long? CachedPromptTokens { get; set; } = null;

        /// <summary>
        /// Completion tokens.
        /// </summary>
        public long CompletionTokens { get; set; } = 0;

        /// <summary>
        /// Activity per group key, in GroupKeys order.
        /// </summary>
        public List<UsageGroupValue> Groups { get; set; } = new List<UsageGroupValue>();

        /// <summary>
        /// Average time per stage.
        /// </summary>
        public List<UsageStageAverage> Stages { get; set; } = new List<UsageStageAverage>();

        /// <summary>
        /// Chat: thumbs up.
        /// </summary>
        public long ThumbsUp { get; set; } = 0;

        /// <summary>
        /// Chat: thumbs down.
        /// </summary>
        public long ThumbsDown { get; set; } = 0;

        /// <summary>
        /// Chat: tool calls that succeeded.
        /// </summary>
        public long ToolCalls { get; set; } = 0;

        /// <summary>
        /// Chat: tool calls that failed.
        /// </summary>
        public long ToolFailures { get; set; } = 0;

        /// <summary>
        /// Chat: tool calls denied.
        /// </summary>
        public long ToolDenied { get; set; } = 0;
    }
}
