namespace Xeno.Sdk
{
    using System;
    using System.Collections.Generic;

    /// <summary>
    /// Provider health strip for a range.
    /// </summary>
    public class ProviderHealthStrip
    {
        /// <summary>
        /// Range start, UTC.
        /// </summary>
        public DateTime FromUtc { get; set; }

        /// <summary>
        /// Range end, UTC.
        /// </summary>
        public DateTime ToUtc { get; set; }

        /// <summary>
        /// Bucket size in minutes.
        /// </summary>
        public int BucketMinutes { get; set; }

        /// <summary>
        /// One row per provider.
        /// </summary>
        public List<ProviderHealthRow> Providers { get; set; } = new List<ProviderHealthRow>();
    }
}
