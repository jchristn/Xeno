namespace Xeno.Core.Analytics
{
    using System;
    using System.Collections.Generic;

    /// <summary>
    /// Totals for the whole range, for KPI tiles.
    /// </summary>
    public sealed class UsageTotals
    {
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
        /// Failed divided by requests.
        /// </summary>
        public double? ErrorRate { get; set; } = null;

        /// <summary>
        /// Network fallbacks; null when unknown.
        /// </summary>
        public long? Fallbacks { get; set; } = null;

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
        /// Prompt tokens, including cached.
        /// </summary>
        public long PromptTokens { get; set; } = 0;

        /// <summary>
        /// Cached prompt tokens; null when none recorded.
        /// </summary>
        public long? CachedPromptTokens { get; set; } = null;

        /// <summary>
        /// Completion tokens.
        /// </summary>
        public long CompletionTokens { get; set; } = 0;

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
