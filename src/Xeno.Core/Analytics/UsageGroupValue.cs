namespace Xeno.Core.Analytics
{
    using System;
    using System.Collections.Generic;

    /// <summary>
    /// One group's activity in one bucket.
    /// </summary>
    public sealed class UsageGroupValue
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
        /// Prompt plus completion tokens.
        /// </summary>
        public long Tokens { get; set; } = 0;
    }
}
