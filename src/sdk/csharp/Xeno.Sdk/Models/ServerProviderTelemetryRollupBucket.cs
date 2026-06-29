namespace Xeno.Sdk
{
    using System;

    /// <summary>
    /// Xeno server provider telemetry rollup bucket DTO.
    /// </summary>
    public class ServerProviderTelemetryRollupBucket
    {
        /// <summary>
        /// Gets or sets the bucket utc.
        /// </summary>
        public DateTime? BucketUtc { get; set; }

        /// <summary>
        /// Gets or sets the minimum.
        /// </summary>
        public double Minimum { get; set; }

        /// <summary>
        /// Gets or sets the average.
        /// </summary>
        public double Average { get; set; }

        /// <summary>
        /// Gets or sets the maximum.
        /// </summary>
        public double Maximum { get; set; }

        /// <summary>
        /// Gets or sets the sample count.
        /// </summary>
        public int SampleCount { get; set; }
    }
}
