namespace Xeno.Core.Analytics
{
    using System;
    using System.Collections.Generic;

    /// <summary>
    /// Health of one provider over one bucket.
    /// </summary>
    public sealed class ProviderHealthCell
    {
        /// <summary>
        /// Bucket start, UTC.
        /// </summary>
        public DateTime StartUtc { get; set; } = DateTime.MinValue;

        /// <summary>
        /// Status.
        /// </summary>
        public ProviderHealthStatusEnum Status { get; set; } = ProviderHealthStatusEnum.NoData;

        /// <summary>
        /// Samples in the bucket.
        /// </summary>
        public int Samples { get; set; } = 0;

        /// <summary>
        /// Unhealthy samples in the bucket.
        /// </summary>
        public int Failures { get; set; } = 0;
    }
}
