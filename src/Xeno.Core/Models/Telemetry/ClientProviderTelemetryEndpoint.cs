namespace Xeno.Core.Models.Telemetry
{
    /// <summary>
    /// Provider telemetry endpoint metadata.
    /// </summary>
    public class ClientProviderTelemetryEndpoint
    {
        /// <summary>
        /// Provider name.
        /// </summary>
        public string ProviderName { get; set; } = string.Empty;

        /// <summary>
        /// Provider API type.
        /// </summary>
        public string ApiType { get; set; } = string.Empty;

        /// <summary>
        /// Provider kind.
        /// </summary>
        public ClientProviderTelemetryKindEnum Kind { get; set; } = ClientProviderTelemetryKindEnum.Unknown;

        /// <summary>
        /// Endpoint display value with secrets redacted.
        /// </summary>
        public string EndpointDisplay { get; set; } = string.Empty;

        /// <summary>
        /// Stable endpoint hash.
        /// </summary>
        public string EndpointHash { get; set; } = string.Empty;

        /// <summary>
        /// Metrics URL used for collection.
        /// </summary>
        public string? MetricsUrl { get; set; } = null;

        /// <summary>
        /// Health URL used for collection.
        /// </summary>
        public string? HealthUrl { get; set; } = null;

        /// <summary>
        /// Models URL used for collection.
        /// </summary>
        public string? ModelsUrl { get; set; } = null;
    }
}
