namespace Xeno.Sdk
{
    using System;
    using System.Collections.Generic;

    /// <summary>
    /// Health over time for one provider.
    /// </summary>
    public class ProviderHealthRow
    {
        /// <summary>
        /// Provider.
        /// </summary>
        public string ProviderName { get; set; } = String.Empty;

        /// <summary>
        /// Client.
        /// </summary>
        public string? ClientId { get; set; }

        /// <summary>
        /// Healthy share; null with no samples.
        /// </summary>
        public double? UptimePercent { get; set; }

        /// <summary>
        /// Failing samples in a row at the end.
        /// </summary>
        public int ConsecutiveFailures { get; set; }

        /// <summary>
        /// One cell per bucket.
        /// </summary>
        public List<ProviderHealthCell> Cells { get; set; } = new List<ProviderHealthCell>();
    }
}
