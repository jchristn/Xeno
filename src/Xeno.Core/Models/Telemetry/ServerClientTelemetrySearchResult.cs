namespace Xeno.Core.Models.Telemetry
{
    using System.Collections.Generic;

    /// <summary>
    /// Server host telemetry search result.
    /// </summary>
    public class ServerClientTelemetrySearchResult
    {
        /// <summary>
        /// Matching host telemetry samples.
        /// </summary>
        public List<ServerClientTelemetrySampleRecord> Items { get; set; } = new List<ServerClientTelemetrySampleRecord>();

        /// <summary>
        /// Total matching row count when available.
        /// </summary>
        public int Total { get; set; } = 0;
    }
}
