namespace Xeno.Core.Models.Telemetry
{
    /// <summary>
    /// Provider telemetry metric source.
    /// </summary>
    public enum ClientProviderTelemetryMetricSourceEnum
    {
        /// <summary>
        /// Unknown source.
        /// </summary>
        Unknown,

        /// <summary>
        /// Provider health endpoint.
        /// </summary>
        Health,

        /// <summary>
        /// Provider model endpoint.
        /// </summary>
        Models,

        /// <summary>
        /// Provider Prometheus endpoint.
        /// </summary>
        Prometheus,

        /// <summary>
        /// Xeno request history.
        /// </summary>
        RequestHistory,

        /// <summary>
        /// Provider cloud headers.
        /// </summary>
        CloudHeaders
    }
}
