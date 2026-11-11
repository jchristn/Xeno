namespace Xeno.Sdk
{
    using System;
    using System.Collections.Generic;

    /// <summary>
    /// One time bucket of usage analytics.
    /// </summary>
    public class UsageBucket
    {
        /// <summary>
        /// Start, UTC.
        /// </summary>
        public DateTime BucketStartUtc { get; set; }

        /// <summary>
        /// End, UTC.
        /// </summary>
        public DateTime BucketEndUtc { get; set; }

        /// <summary>
        /// Requests.
        /// </summary>
        public long Requests { get; set; }

        /// <summary>
        /// Succeeded.
        /// </summary>
        public long Succeeded { get; set; }

        /// <summary>
        /// Failed.
        /// </summary>
        public long Failed { get; set; }

        /// <summary>
        /// Network fallbacks; null when unknown.
        /// </summary>
        public long? Fallbacks { get; set; }

        /// <summary>
        /// Total milliseconds.
        /// </summary>
        public UsageDistribution Latency { get; set; } = new UsageDistribution();

        /// <summary>
        /// Milliseconds to first token.
        /// </summary>
        public UsageDistribution TimeToFirstToken { get; set; } = new UsageDistribution();

        /// <summary>
        /// Streaming milliseconds.
        /// </summary>
        public UsageDistribution Streaming { get; set; } = new UsageDistribution();

        /// <summary>
        /// Tokens per second.
        /// </summary>
        public UsageDistribution Throughput { get; set; } = new UsageDistribution();

        /// <summary>
        /// Prompt tokens not known to be cached.
        /// </summary>
        public long PromptTokens { get; set; }

        /// <summary>
        /// Cached prompt tokens; null when none recorded.
        /// </summary>
        public long? CachedPromptTokens { get; set; }

        /// <summary>
        /// Completion tokens.
        /// </summary>
        public long CompletionTokens { get; set; }

        /// <summary>
        /// Activity per group key.
        /// </summary>
        public List<UsageGroupValue> Groups { get; set; } = new List<UsageGroupValue>();

        /// <summary>
        /// Average per stage.
        /// </summary>
        public List<UsageStageAverage> Stages { get; set; } = new List<UsageStageAverage>();

        /// <summary>
        /// Chat: thumbs up.
        /// </summary>
        public long ThumbsUp { get; set; }

        /// <summary>
        /// Chat: thumbs down.
        /// </summary>
        public long ThumbsDown { get; set; }

        /// <summary>
        /// Chat: tool calls that ran.
        /// </summary>
        public long ToolCalls { get; set; }

        /// <summary>
        /// Chat: tool calls that failed.
        /// </summary>
        public long ToolFailures { get; set; }

        /// <summary>
        /// Chat: tool calls denied.
        /// </summary>
        public long ToolDenied { get; set; }
    }
}
