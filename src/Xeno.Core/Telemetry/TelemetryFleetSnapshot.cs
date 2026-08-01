namespace Xeno.Core.Telemetry
{
    using System.Collections.Generic;

    /// <summary>
    /// Snapshot of the latest fleet telemetry, sampled by observable gauges.
    /// </summary>
    public class TelemetryFleetSnapshot
    {
        /// <summary>
        /// Per-client host resource gauges.
        /// </summary>
        public IReadOnlyList<FleetHostGauge> Hosts
        {
            get => _Hosts;
            set => _Hosts = value ?? new List<FleetHostGauge>();
        }

        /// <summary>
        /// Per-client provider gauges.
        /// </summary>
        public IReadOnlyList<FleetProviderGauge> Providers
        {
            get => _Providers;
            set => _Providers = value ?? new List<FleetProviderGauge>();
        }

        private IReadOnlyList<FleetHostGauge> _Hosts = new List<FleetHostGauge>();
        private IReadOnlyList<FleetProviderGauge> _Providers = new List<FleetProviderGauge>();

        /// <summary>
        /// Instantiate.
        /// </summary>
        public TelemetryFleetSnapshot()
        {
        }
    }
}
