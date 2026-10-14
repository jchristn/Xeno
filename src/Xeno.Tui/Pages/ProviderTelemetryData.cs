namespace Xeno.Tui.Pages
{
    using System.Collections.Generic;
    using Xeno.Client.Presentation;
    using Xeno.Client.Telemetry.Models;

    /// <summary>
    /// Data loaded for the Provider Telemetry page.
    /// </summary>
    internal sealed class ProviderTelemetryData
    {
        /// <summary>
        /// Latest rows per provider.
        /// </summary>
        public List<ProviderSampleRow> Latest { get; }

        /// <summary>
        /// The chosen metric over time.
        /// </summary>
        public XenoChart Chart { get; }

        /// <summary>
        /// Samples.
        /// </summary>
        public List<ProviderSampleRow> Samples { get; }

        /// <summary>
        /// Configured provider names.
        /// </summary>
        public List<string> Providers { get; }

        /// <summary>
        /// Create the data.
        /// </summary>
        /// <param name="latest">Latest rows.</param>
        /// <param name="chart">Buckets.</param>
        /// <param name="samples">Samples.</param>
        /// <param name="providers">Provider names.</param>
        public ProviderTelemetryData(List<ProviderSampleRow> latest, XenoChart chart, List<ProviderSampleRow> samples, List<string> providers)
        {
            Latest = latest;
            Chart = chart;
            Samples = samples;
            Providers = providers;
        }
    }
}
