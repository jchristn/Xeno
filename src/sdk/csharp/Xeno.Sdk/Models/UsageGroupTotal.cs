namespace Xeno.Sdk
{
    using System;
    using System.Collections.Generic;

    /// <summary>
    /// One group's totals for the whole range.
    /// </summary>
    public class UsageGroupTotal
    {
        /// <summary>
        /// Group key.
        /// </summary>
        public string Key { get; set; } = String.Empty;

        /// <summary>
        /// Requests.
        /// </summary>
        public long Requests { get; set; }

        /// <summary>
        /// Failed requests.
        /// </summary>
        public long Failed { get; set; }

        /// <summary>
        /// Prompt tokens.
        /// </summary>
        public long PromptTokens { get; set; }

        /// <summary>
        /// Completion tokens.
        /// </summary>
        public long CompletionTokens { get; set; }

        /// <summary>
        /// Total latency.
        /// </summary>
        public UsageDistribution Latency { get; set; } = new UsageDistribution();
    }
}
