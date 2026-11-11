namespace Xeno.Sdk
{
    using System;
    using System.Collections.Generic;

    /// <summary>
    /// Query for the provider health strip. Null values are left out.
    /// </summary>
    public class ProviderHealthQuery
    {
        /// <summary>
        /// Range start, UTC.
        /// </summary>
        public DateTime? FromUtc { get; set; }

        /// <summary>
        /// Range end, UTC.
        /// </summary>
        public DateTime? ToUtc { get; set; }

        /// <summary>
        /// Bucket minutes.
        /// </summary>
        public int? BucketMinutes { get; set; }

        /// <summary>
        /// Only this client.
        /// </summary>
        public string? ClientId { get; set; }

        /// <summary>
        /// Only this provider.
        /// </summary>
        public string? ProviderName { get; set; }
    }
}
