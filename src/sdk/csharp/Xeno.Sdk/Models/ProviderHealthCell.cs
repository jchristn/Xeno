namespace Xeno.Sdk
{
    using System;
    using System.Collections.Generic;

    /// <summary>
    /// Health of one provider over one bucket.
    /// </summary>
    public class ProviderHealthCell
    {
        /// <summary>
        /// Bucket start, UTC.
        /// </summary>
        public DateTime StartUtc { get; set; }

        /// <summary>
        /// Healthy, Degraded, Unhealthy, or NoData.
        /// </summary>
        public string Status { get; set; } = String.Empty;

        /// <summary>
        /// Samples.
        /// </summary>
        public int Samples { get; set; }

        /// <summary>
        /// Unhealthy samples.
        /// </summary>
        public int Failures { get; set; }
    }
}
