namespace Xeno.Client.Presentation
{
    using System;

    /// <summary>
    /// The time window one chart column covers, for drill-down.
    /// </summary>
    public sealed class XenoChartBucket
    {
        /// <summary>
        /// Create a bucket.
        /// </summary>
        /// <param name="startUtc">Start, UTC.</param>
        /// <param name="endUtc">End, UTC.</param>
        public XenoChartBucket(DateTime startUtc, DateTime endUtc)
        {
            StartUtc = startUtc;
            EndUtc = endUtc;
        }

        /// <summary>
        /// Start, UTC.
        /// </summary>
        public DateTime StartUtc { get; }

        /// <summary>
        /// End, UTC.
        /// </summary>
        public DateTime EndUtc { get; }
    }
}
