namespace Xeno.Sdk
{
    using System;
    using System.Collections.Generic;

    /// <summary>
    /// Xeno request history entry DTO.
    /// </summary>
    public class RequestHistoryEntry
    {
        /// <summary>
        /// Gets or sets the id.
        /// </summary>
        public string Id { get; set; } = String.Empty;

        /// <summary>
        /// Gets or sets the tenant id.
        /// </summary>
        public string TenantId { get; set; } = String.Empty;

        /// <summary>
        /// Gets or sets the requestor tenant id.
        /// </summary>
        public string RequestorTenantId { get; set; } = String.Empty;

        /// <summary>
        /// Gets or sets the requestor principal type.
        /// </summary>
        public string RequestorPrincipalType { get; set; } = String.Empty;

        /// <summary>
        /// Gets or sets the requestor principal id.
        /// </summary>
        public string RequestorPrincipalId { get; set; } = String.Empty;

        /// <summary>
        /// Gets or sets the path.
        /// </summary>
        public string Path { get; set; } = String.Empty;

        /// <summary>
        /// Gets or sets the method.
        /// </summary>
        public string Method { get; set; } = String.Empty;

        /// <summary>
        /// Gets or sets the model.
        /// </summary>
        public string Model { get; set; } = String.Empty;

        /// <summary>
        /// Gets or sets the selected client id.
        /// </summary>
        public string SelectedClientId { get; set; } = String.Empty;

        /// <summary>
        /// Gets or sets the status code.
        /// </summary>
        public int StatusCode { get; set; }

        /// <summary>
        /// Gets or sets the duration ms.
        /// </summary>
        public long DurationMs { get; set; }

        /// <summary>
        /// Gets or sets the created utc.
        /// </summary>
        public DateTime? CreatedUtc { get; set; }

        /// <summary>
        /// Gets or sets the completed utc.
        /// </summary>
        public DateTime? CompletedUtc { get; set; }

        /// <summary>
        /// Gets or sets the request headers.
        /// </summary>
        public Dictionary<string, string> RequestHeaders { get; set; } = new Dictionary<string, string>();

        /// <summary>
        /// Gets or sets the response headers.
        /// </summary>
        public Dictionary<string, string> ResponseHeaders { get; set; } = new Dictionary<string, string>();

        /// <summary>
        /// Gets or sets the request body.
        /// </summary>
        public string RequestBody { get; set; } = String.Empty;

        /// <summary>
        /// Gets or sets the response body.
        /// </summary>
        public string ResponseBody { get; set; } = String.Empty;

        /// <summary>
        /// Gets or sets the request body length.
        /// </summary>
        public long RequestBodyLength { get; set; }

        /// <summary>
        /// Gets or sets the response body length.
        /// </summary>
        public long ResponseBodyLength { get; set; }

        /// <summary>
        /// Gets or sets the request body truncated.
        /// </summary>
        public bool RequestBodyTruncated { get; set; }

        /// <summary>
        /// Gets or sets the response body truncated.
        /// </summary>
        public bool ResponseBodyTruncated { get; set; }

        /// <summary>
        /// Gets or sets the telemetry.
        /// </summary>
        public RequestHistoryTelemetry? Telemetry { get; set; }

        /// <summary>
        /// Gets or sets the timeline.
        /// </summary>
        public List<RequestHistoryTimelineEvent> Timeline { get; set; } = new List<RequestHistoryTimelineEvent>();

        /// <summary>
        /// Gets or sets the routing decision.
        /// </summary>
        public RoutingDecision? RoutingDecision { get; set; }
    }
}
