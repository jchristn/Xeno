namespace Xeno.Sdk
{
    using System;
    using System.Collections.Generic;

    /// <summary>
    /// Average and 95th percentile of one stage.
    /// </summary>
    public class UsageStageAverage
    {
        /// <summary>
        /// Stage code.
        /// </summary>
        public string Stage { get; set; } = String.Empty;

        /// <summary>
        /// Records with this stage.
        /// </summary>
        public int Count { get; set; }

        /// <summary>
        /// Average milliseconds.
        /// </summary>
        public double? AverageMs { get; set; }

        /// <summary>
        /// 95th percentile milliseconds.
        /// </summary>
        public double? P95Ms { get; set; }
    }
}
