namespace Xeno.Client.LocalDashboard.Models
{
    using System;
    using System.Collections.Generic;

    /// <summary>
    /// Local request history entry.
    /// </summary>
    public class LocalRequestHistoryEntry
    {
        /// <summary>
        /// Request identifier.
        /// </summary>
        public string Id { get; set; } = string.Empty;

        /// <summary>
        /// Tenant identifier.
        /// </summary>
        public string? TenantId { get; set; } = null;

        /// <summary>
        /// Client identifier.
        /// </summary>
        public string? ClientId { get; set; } = null;

        /// <summary>
        /// Request received timestamp.
        /// </summary>
        public DateTime ReceivedUtc { get; set; } = DateTime.UtcNow;

        /// <summary>
        /// Request completed timestamp.
        /// </summary>
        public DateTime? CompletedUtc { get; set; } = null;

        /// <summary>
        /// HTTP method.
        /// </summary>
        public string Method { get; set; } = "POST";

        /// <summary>
        /// Request path.
        /// </summary>
        public string Path { get; set; } = "/";

        /// <summary>
        /// Requestor IP.
        /// </summary>
        public string? RequestorIp { get; set; } = null;

        /// <summary>
        /// Selected provider name.
        /// </summary>
        public string? ProviderName { get; set; } = null;

        /// <summary>
        /// Selected provider API type.
        /// </summary>
        public string? ApiType { get; set; } = null;

        /// <summary>
        /// Requested model name.
        /// </summary>
        public string? ModelName { get; set; } = null;

        /// <summary>
        /// Provider telemetry sample ID captured near request start.
        /// </summary>
        public string? ProviderTelemetrySampleId { get; set; } = null;

        /// <summary>
        /// Provider telemetry health status captured near request start.
        /// </summary>
        public string? ProviderTelemetryHealthStatus { get; set; } = null;

        /// <summary>
        /// Provider telemetry queue depth captured near request start.
        /// </summary>
        public double? ProviderTelemetryQueuedRequests { get; set; } = null;

        /// <summary>
        /// HTTP status code.
        /// </summary>
        public int? StatusCode { get; set; } = null;

        /// <summary>
        /// Request outcome.
        /// </summary>
        public LocalRequestOutcomeEnum Outcome { get; set; } = LocalRequestOutcomeEnum.Processing;

        /// <summary>
        /// Error message.
        /// </summary>
        public string? Error { get; set; } = null;

        /// <summary>
        /// Whether response was streamed.
        /// </summary>
        public bool Streamed { get; set; } = false;

        /// <summary>
        /// Request headers.
        /// </summary>
        public Dictionary<string, string> RequestHeaders { get; set; } = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        /// <summary>
        /// Response headers.
        /// </summary>
        public Dictionary<string, string> ResponseHeaders { get; set; } = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        /// <summary>
        /// Captured request body.
        /// </summary>
        public string? RequestBody { get; set; } = null;

        /// <summary>
        /// Captured response body.
        /// </summary>
        public string? ResponseBody { get; set; } = null;

        /// <summary>
        /// Telemetry.
        /// </summary>
        public LocalRequestTelemetry Telemetry { get; set; } = new LocalRequestTelemetry();

        /// <summary>
        /// Timeline events.
        /// </summary>
        public List<LocalRequestTimelineEvent> Timeline { get; set; } = new List<LocalRequestTimelineEvent>();
    }
}
