namespace Xeno.Core.Analytics
{
    using System;
    using System.Collections.Generic;

    /// <summary>
    /// One group's totals for the whole range.
    /// </summary>
    public sealed class UsageGroupTotal
    {
        /// <summary>
        /// Group key: a value, Other, or Unknown.
        /// </summary>
        public string Key { get; set; } = String.Empty;

        /// <summary>
        /// Requests.
        /// </summary>
        public long Requests { get; set; } = 0;

        /// <summary>
        /// Failed requests.
        /// </summary>
        public long Failed { get; set; } = 0;

        /// <summary>
        /// Prompt tokens.
        /// </summary>
        public long PromptTokens { get; set; } = 0;

        /// <summary>
        /// Completion tokens.
        /// </summary>
        public long CompletionTokens { get; set; } = 0;

        /// <summary>
        /// Total latency.
        /// </summary>
        public UsageDistribution Latency { get; set; } = new UsageDistribution();
    }
}
