namespace Xeno.Sdk
{
    using System;
    using System.Collections.Generic;

    /// <summary>
    /// One group's activity in one bucket.
    /// </summary>
    public class UsageGroupValue
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
        /// Prompt plus completion tokens.
        /// </summary>
        public long Tokens { get; set; }
    }
}
