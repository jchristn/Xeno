namespace Xeno.Tui.Pages
{
    using System.Collections.Generic;
    using Xeno.Client.Presentation;
    using Xeno.Client.Telemetry.Models;

    /// <summary>
    /// Data loaded for the Host Telemetry page.
    /// </summary>
    internal sealed class HostTelemetryData
    {
        /// <summary>
        /// Latest sample, or null.
        /// </summary>
        public ClientTelemetrySampleDetail? Latest { get; }

        /// <summary>
        /// Storage status.
        /// </summary>
        public ClientTelemetryPersistenceStatus Status { get; }

        /// <summary>
        /// CPU, memory, and GPU over time.
        /// </summary>
        public List<XenoChart> Charts { get; }

        /// <summary>
        /// Samples.
        /// </summary>
        public List<HostSampleRow> Samples { get; }

        /// <summary>
        /// Create the data.
        /// </summary>
        /// <param name="latest">Latest sample.</param>
        /// <param name="status">Status.</param>
        /// <param name="charts">Charts.</param>
        /// <param name="samples">Samples.</param>
        public HostTelemetryData(ClientTelemetrySampleDetail? latest, ClientTelemetryPersistenceStatus status, List<XenoChart> charts, List<HostSampleRow> samples)
        {
            Latest = latest;
            Status = status;
            Charts = charts;
            Samples = samples;
        }
    }
}
