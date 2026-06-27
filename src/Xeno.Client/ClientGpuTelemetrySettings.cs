namespace Xeno.Client
{
    using System;

    /// <summary>
    /// Xeno client GPU telemetry settings.
    /// </summary>
    public class ClientGpuTelemetrySettings
    {
        private int _CommandTimeoutMs = 5000;

        /// <summary>
        /// Whether GPU telemetry collection is enabled.
        /// </summary>
        public bool Enabled { get; set; } = true;

        /// <summary>
        /// Optional NVIDIA DCGM exporter URL.
        /// </summary>
        public string? NvidiaDcgmExporterUrl { get; set; } = null;

        /// <summary>
        /// GPU command timeout in milliseconds.
        /// </summary>
        public int CommandTimeoutMs
        {
            get
            {
                return _CommandTimeoutMs;
            }
            set
            {
                _CommandTimeoutMs = Math.Clamp(value, 1000, 60000);
            }
        }
    }
}
