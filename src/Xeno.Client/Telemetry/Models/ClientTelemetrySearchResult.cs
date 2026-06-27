namespace Xeno.Client.Telemetry.Models
{
    using System.Collections.Generic;

    /// <summary>
    /// Telemetry history search result.
    /// </summary>
    public class ClientTelemetrySearchResult
    {
        /// <summary>
        /// Total matching rows.
        /// </summary>
        public int Total { get; set; } = 0;

        /// <summary>
        /// Returned telemetry samples.
        /// </summary>
        public List<ClientTelemetrySampleRecord> Samples { get; set; } = new List<ClientTelemetrySampleRecord>();
    }
}
