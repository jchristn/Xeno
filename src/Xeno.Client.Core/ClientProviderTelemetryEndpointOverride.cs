namespace Xeno.Client
{
    /// <summary>
    /// Provider telemetry endpoint override.
    /// </summary>
    public class ClientProviderTelemetryEndpointOverride
    {
        /// <summary>
        /// Provider endpoint name.
        /// </summary>
        public string ProviderName { get; set; } = string.Empty;

        /// <summary>
        /// Whether telemetry is enabled for this provider.
        /// </summary>
        public bool Enabled { get; set; } = true;

        /// <summary>
        /// Optional provider telemetry kind override.
        /// </summary>
        public string? ProviderKind { get; set; } = null;

        /// <summary>
        /// Optional metrics endpoint URL.
        /// </summary>
        public string? MetricsUrl { get; set; } = null;

        /// <summary>
        /// Optional health endpoint URL.
        /// </summary>
        public string? HealthUrl { get; set; } = null;

        /// <summary>
        /// Optional models endpoint URL.
        /// </summary>
        public string? ModelsUrl { get; set; } = null;
    }
}
