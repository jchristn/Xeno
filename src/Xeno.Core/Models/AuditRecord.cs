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
        /// HTTP status code.
        /// </summary>
        public int StatusCode { get; set; } = 200;

        /// <summary>
        /// Created timestamp.
        /// </summary>
        public DateTime CreatedUtc { get; set; } = DateTime.UtcNow;
    }
}
