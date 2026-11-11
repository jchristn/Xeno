namespace Xeno.Core.Analytics
{
    using System;
    using System.Collections.Generic;

    /// <summary>
    /// Health over time for one provider.
    /// </summary>
    public sealed class ProviderHealthRow
    {
        /// <summary>
        /// Provider name.
        /// </summary>
        public string ProviderName { get; set; } = String.Empty;

        /// <summary>
        /// Client the provider belongs to (server).
        /// </summary>
        public string? ClientId { get; set; } = null;

        /// <summary>
        /// Healthy samples as a percentage of all samples; null with no samples.
        /// </summary>
        public double? UptimePercent { get; set; } = null;

        /// <summary>
        /// Unhealthy samples in a row at the end of the range.
        /// </summary>
        public int ConsecutiveFailures { get; set; } = 0;

        /// <summary>
        /// One cell per bucket.
        /// </summary>
        public List<ProviderHealthCell> Cells { get; set; } = new List<ProviderHealthCell>();
    }
}
