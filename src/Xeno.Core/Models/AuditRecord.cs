namespace Xeno.Core.Models
{
    using System;
    using Xeno.Core.Enums;
    using Xeno.Core.Helpers;

    /// <summary>
    /// Security audit record.
    /// </summary>
    public class AuditRecord
    {
        /// <summary>
        /// Audit record ID.
        /// </summary>
        public string Id { get; set; } = IdGenerator.NewId("aud_");

        /// <summary>
        /// Tenant ID.
        /// </summary>
        public string TenantId { get; set; } = Constants.DefaultTenantId;

        /// <summary>
        /// Target tenant ID for the audited operation.
        /// </summary>
        public string? TargetTenantId { get; set; } = null;

        /// <summary>
        /// Target resource ID for the audited operation.
        /// </summary>
        public string? TargetResourceId { get; set; } = null;

        /// <summary>
        /// Request ID.
        /// </summary>
        public string RequestId { get; set; } = String.Empty;

        /// <summary>
        /// Principal type.
        /// </summary>
        public PrincipalTypeEnum PrincipalType { get; set; } = PrincipalTypeEnum.None;

        /// <summary>
        /// Principal ID.
        /// </summary>
        public string? PrincipalId { get; set; } = null;

        /// <summary>
        /// Authentication scheme.
        /// </summary>
        public AuthSchemeEnum AuthScheme { get; set; } = AuthSchemeEnum.None;

        /// <summary>
        /// Resource type.
        /// </summary>
        public string ResourceType { get; set; } = String.Empty;

        /// <summary>
        /// Operation type.
        /// </summary>
        public OperationTypeEnum OperationType { get; set; } = OperationTypeEnum.Read;

        /// <summary>
        /// Whether access was allowed.
        /// </summary>
        public bool Allowed { get; set; } = true;

        /// <summary>
        /// Reason code.
        /// </summary>
        public string ReasonCode { get; set; } = "Allowed";

        /// <summary>
        /// Source IP address.
        /// </summary>
        public string? SourceIp { get; set; } = null;

        /// <summary>
        /// X-Forwarded-For header value.
        /// </summary>
        public string? ForwardedFor { get; set; } = null;

        /// <summary>
        /// User-Agent header value.
        /// </summary>
        public string? UserAgent { get; set; } = null;

        /// <summary>
        /// Origin header value.
        /// </summary>
        public string? Origin { get; set; } = null;

        /// <summary>
        /// Referer header value.
        /// </summary>
        public string? Referer { get; set; } = null;

        /// <summary>
        /// Host header value.
        /// </summary>
        public string? Host { get; set; } = null;

        /// <summary>
        /// HTTP request method.
        /// </summary>
        public string? RequestMethod { get; set; } = null;

        /// <summary>
        /// HTTP request path.
        /// </summary>
        public string? RequestPath { get; set; } = null;

        /// <summary>
        /// Optional caller-supplied correlation identifier.
        /// </summary>
        public string? CorrelationId { get; set; } = null;

        /// <summary>
        /// HTTP status code.
        /// </summary>
        public int StatusCode { get; set; } = 200;

        /// <summary>
        /// Created timestamp.
        /// </summary>
        public DateTime CreatedUtc { get; set; } = DateTime.UtcNow;
    }
}
