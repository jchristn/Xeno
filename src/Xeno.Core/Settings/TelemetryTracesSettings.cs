namespace Xeno.Core.Settings
{
    using System;

    /// <summary>
    /// Distributed tracing telemetry settings.
    /// </summary>
    public class TelemetryTracesSettings
    {
        /// <summary>
        /// Enable trace export.
        /// </summary>
        public bool Enable { get; set; } = true;

        /// <summary>
        /// Head sampling ratio. Default value is 1.0 (sample all).
        /// Minimum value is 0.0. Maximum value is 1.0.
        /// </summary>
        public double SamplingRatio
        {
            get => _SamplingRatio;
            set => _SamplingRatio = value < 0.0 ? 0.0 : (value > 1.0 ? 1.0 : value);
        }

        /// <summary>
        /// Propagate W3C trace context to the selected client on relayed requests.
        /// Default value is true.
        /// </summary>
        public bool PropagateToClient { get; set; } = true;

        private double _SamplingRatio = 1.0;

        /// <summary>
        /// Instantiate.
        /// </summary>
        public TelemetryTracesSettings()
        {
        }
    }
}
