namespace Xeno.Core.Analytics
{
    using System;

    /// <summary>
    /// Bucket rules shared by every usage chart: the dashboard presets (last hour 1 minute x 60, last day 15 minutes x
    /// 96, last week 2 hours x 84, last month 6 hours x 120) and the limits every analytics route enforces.
    /// </summary>
    public static class UsageRanges
    {
        /// <summary>
        /// Most buckets one query may ask for.
        /// </summary>
        public const int MaxBuckets = 2000;

        /// <summary>
        /// Bucket minutes for the last hour.
        /// </summary>
        public const int HourBucketMinutes = 1;

        /// <summary>
        /// Bucket minutes for the last day.
        /// </summary>
        public const int DayBucketMinutes = 15;

        /// <summary>
        /// Bucket minutes for the last week.
        /// </summary>
        public const int WeekBucketMinutes = 120;

        /// <summary>
        /// Bucket minutes for the last month (30 days).
        /// </summary>
        public const int MonthBucketMinutes = 360;

        /// <summary>
        /// Number of buckets a range and bucket size produce.
        /// </summary>
        /// <param name="fromUtc">Start.</param>
        /// <param name="toUtc">End.</param>
        /// <param name="bucketMinutes">Bucket minutes.</param>
        /// <returns>Bucket count, at least 1.</returns>
        public static int BucketCount(DateTime fromUtc, DateTime toUtc, int bucketMinutes)
        {
            double minutes = (toUtc - fromUtc).TotalMinutes;
            return Math.Max(1, (int)Math.Ceiling(minutes / Math.Max(1, bucketMinutes) - 1e-9));
        }

        /// <summary>
        /// Check a range: the end must follow the start and the bucket count must not exceed <see cref="MaxBuckets"/>.
        /// </summary>
        /// <param name="fromUtc">Start.</param>
        /// <param name="toUtc">End.</param>
        /// <param name="bucketMinutes">Bucket minutes.</param>
        /// <exception cref="ArgumentException">The range or bucket size is not valid.</exception>
        public static void Validate(DateTime fromUtc, DateTime toUtc, int bucketMinutes)
        {
            if (bucketMinutes < 1) throw new ArgumentException("bucketMinutes must be at least 1.");
            if (toUtc <= fromUtc) throw new ArgumentException("toUtc must be later than fromUtc.");
            if (BucketCount(fromUtc, toUtc, bucketMinutes) > MaxBuckets) throw new ArgumentException("The range needs more than " + MaxBuckets + " buckets; use larger buckets or a shorter range.");
        }

        /// <summary>
        /// Default bucket minutes for a range, matching the dashboard presets.
        /// </summary>
        /// <param name="fromUtc">Start.</param>
        /// <param name="toUtc">End.</param>
        /// <returns>Bucket minutes.</returns>
        public static int DefaultBucketMinutes(DateTime fromUtc, DateTime toUtc)
        {
            double minutes = Math.Max(1, (toUtc - fromUtc).TotalMinutes);
            if (minutes <= 60) return HourBucketMinutes;
            if (minutes <= 24 * 60) return DayBucketMinutes;
            if (minutes <= 7 * 24 * 60) return WeekBucketMinutes;
            if (minutes <= 30 * 24 * 60) return MonthBucketMinutes;
            return Math.Max(MonthBucketMinutes, (int)Math.Ceiling(minutes / 120));
        }
    }
}
