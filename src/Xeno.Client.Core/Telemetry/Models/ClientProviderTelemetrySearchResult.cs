namespace Xeno.Client.Telemetry.Models
{
    using System.Collections.Generic;

    /// <summary>
    /// Provider telemetry search result.
    /// </summary>
    public class ClientProviderTelemetrySearchResult
    {
        /// <summary>
        /// Returned samples.
        /// </summary>
        public List<ClientProviderTelemetrySampleRecord> Samples { get; set; } = new List<ClientProviderTelemetrySampleRecord>();

        /// <summary>
        /// Total records.
        /// </summary>
        public int Total { get; set; } = 0;

        /// <summary>
        /// Limit.
        /// </summary>
        public int Limit { get; set; } = 100;

        /// <summary>
        /// Offset.
        /// </summary>
        public int Offset { get; set; } = 0;
    }
}
