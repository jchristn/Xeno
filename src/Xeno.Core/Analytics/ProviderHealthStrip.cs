namespace Xeno.Core.Analytics
{
    using System;
    using System.Collections.Generic;

    /// <summary>
    /// Provider health strip for a range.
    /// </summary>
    public sealed class ProviderHealthStrip
    {
        /// <summary>
        /// Range start, UTC.
        /// </summary>
        public DateTime FromUtc { get; set; } = DateTime.MinValue;

        /// <summary>
        /// Range end, UTC.
        /// </summary>
        public DateTime ToUtc { get; set; } = DateTime.MinValue;

        /// <summary>
        /// Bucket size in minutes.
        /// </summary>
        public int BucketMinutes { get; set; } = 0;

        /// <summary>
        /// One row per provider.
        /// </summary>
        public List<ProviderHealthRow> Providers { get; set; } = new List<ProviderHealthRow>();
    }
}
