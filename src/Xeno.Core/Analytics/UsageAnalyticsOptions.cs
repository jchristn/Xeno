namespace Xeno.Core.Analytics
{
    using System;
    using System.Collections.Generic;

    /// <summary>
    /// Builder options. Defaults suit dashboards; callers pass their own from settings.
    /// </summary>
    public sealed class UsageAnalyticsOptions
    {
        /// <summary>
        /// Records read before stopping and setting Truncated. Default 250,000.
        /// </summary>
        public int MaxRecords { get; set; } = 250000;

        /// <summary>
        /// Group keys shown before folding the rest into Other. Default 8.
        /// </summary>
        public int DefaultTopN { get; set; } = 8;
    }
}
