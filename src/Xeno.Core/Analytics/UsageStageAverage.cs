namespace Xeno.Core.Analytics
{
    using System;
    using System.Collections.Generic;

    /// <summary>
    /// Average and 95th percentile of one stage.
    /// </summary>
    public sealed class UsageStageAverage
    {
        /// <summary>
        /// Stage.
        /// </summary>
        public UsageStageEnum Stage { get; set; } = UsageStageEnum.Other;

        /// <summary>
        /// Records with this stage.
        /// </summary>
        public int Count { get; set; } = 0;

        /// <summary>
        /// Average milliseconds.
        /// </summary>
        public double? AverageMs { get; set; } = null;

        /// <summary>
        /// 95th percentile milliseconds.
        /// </summary>
        public double? P95Ms { get; set; } = null;
    }
}
