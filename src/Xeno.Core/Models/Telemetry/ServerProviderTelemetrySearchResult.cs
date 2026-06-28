namespace Xeno.Core.Models.Telemetry
{
    using System.Collections.Generic;

    /// <summary>
    /// Server provider telemetry search result.
    /// </summary>
    public class ServerProviderTelemetrySearchResult
    {
        /// <summary>
        /// Matching provider telemetry samples.
        /// </summary>
        public List<ServerProviderTelemetrySampleRecord> Items { get; set; } = new List<ServerProviderTelemetrySampleRecord>();

        /// <summary>
        /// Total matching row count when available.
        /// </summary>
        public int Total { get; set; } = 0;
    }
}
