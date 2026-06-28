namespace Xeno.Core.Models
{
    using System;
    using System.Collections.Generic;
    using Xeno.Core.Enums;
    using Xeno.Core.Helpers;

    /// <summary>
    /// Request history entry.
    /// </summary>
    public class RequestHistoryEntry
    {
        /// <summary>
        /// Entry ID.
        /// </summary>
        public string Id { get; set; } = IdGenerator.NewRequestHistoryId();

        /// <summary>
        /// Tenant ID.
        /// </summary>
        public string TenantId { get; set; } = Constants.DefaultTenantId;

        /// <summary>
        /// Tenant ID of the authenticated requestor.
        /// </summary>
        public string RequestorTenantId { get; set; } = Constants.DefaultTenantId;

        /// <summary>
        /// Request path.
        /// </summary>
        public string Path { get; set; } = String.Empty;

        /// <summary>
        /// HTTP method.
        /// </summary>
        public string Method { get; set; } = String.Empty;

        /// <summary>
        /// Effective model.
        /// </summary>
        public string? Model { get; set; } = null;

        /// <summary>
        /// Selected client ID.
        /// </summary>
        public string? SelectedClientId { get; set; } = null;

        /// <summary>
        /// HTTP status code.
        /// </summary>
        public int StatusCode { get; set; } = 200;

        /// <summary>
        /// Request duration in milliseconds.
        /// </summary>
        public long DurationMs { get; set; } = 0;

        /// <summary>
        /// Request source IP address.
        /// </summary>
        public string? SourceIp { get; set; } = null;

        /// <summary>
        /// Authenticated user ID.
        /// </summary>
        public string? UserId { get; set; } = null;

        /// <summary>
        /// Authenticated credential ID.
        /// </summary>
        public string? CredentialId { get; set; } = null;

        /// <summary>
        /// Authenticated session ID.
        /// </summary>
        public string? SessionId { get; set; } = null;

        /// <summary>
        /// Principal type used for the request.
        /// </summary>
        public PrincipalTypeEnum PrincipalType { get; set; } = PrincipalTypeEnum.None;

        /// <summary>
        /// Resolved principal type of the authenticated requestor.
        /// </summary>
        public PrincipalTypeEnum RequestorPrincipalType { get; set; } = PrincipalTypeEnum.None;

        /// <summary>
        /// Resolved principal ID of the authenticated requestor.
        /// </summary>
        public string? RequestorPrincipalId { get; set; } = null;

        /// <summary>
        /// Authentication scheme used for the request.
        /// </summary>
        public AuthSchemeEnum AuthScheme { get; set; } = AuthSchemeEnum.None;

        /// <summary>
        /// Created timestamp.
        /// </summary>
        public DateTime CreatedUtc { get; set; } = DateTime.UtcNow;

        /// <summary>
        /// Completed timestamp.
        /// </summary>
        public DateTime? CompletedUtc { get; set; } = null;

        /// <summary>
        /// Captured request headers.
        /// </summary>
        public Dictionary<string, string> RequestHeaders { get; set; } = new Dictionary<string, string>();

        /// <summary>
        /// Captured response headers.
        /// </summary>
        public Dictionary<string, string> ResponseHeaders { get; set; } = new Dictionary<string, string>();

        /// <summary>
        /// Captured request body.
        /// </summary>
        public string? RequestBody { get; set; } = null;

        /// <summary>
        /// Captured response body.
        /// </summary>
        public string? ResponseBody { get; set; } = null;

        /// <summary>
        /// Request body size in bytes.
        /// </summary>
        public long RequestBodyLength { get; set; } = 0;

        /// <summary>
        /// Response body size in bytes.
        /// </summary>
        public long ResponseBodyLength { get; set; } = 0;

        /// <summary>
        /// Whether the captured request body was truncated.
        /// </summary>
        public bool RequestBodyTruncated { get; set; } = false;

        /// <summary>
        /// Whether the captured response body was truncated.
        /// </summary>
        public bool ResponseBodyTruncated { get; set; } = false;

        /// <summary>
        /// Captured relay and provider telemetry.
        /// </summary>
        public RequestHistoryTelemetry Telemetry { get; set; } = new RequestHistoryTelemetry();

        /// <summary>
        /// Timestamped request timeline.
        /// </summary>
        public List<RequestHistoryTimelineEvent> Timeline { get; set; } = new List<RequestHistoryTimelineEvent>();

        /// <summary>
        /// Routing decision.
        /// </summary>
        public RoutingDecision? RoutingDecision { get; set; } = null;
    }
}
