namespace Xeno.Core.Models
{
    using System;
    using System.Collections.Generic;
    using Xeno.Core.Enums;
    using Xeno.Core.Helpers;

    /// <summary>
    /// Request context.
    /// </summary>
    public class RequestContext
    {
        /// <summary>
        /// Request ID.
        /// </summary>
        public string RequestId { get; set; } = IdGenerator.NewRequestHistoryId();

        /// <summary>
        /// Request start timestamp.
        /// </summary>
        public DateTime StartedUtc { get; set; } = DateTime.UtcNow;

        /// <summary>
        /// Tenant ID.
        /// </summary>
        public string TenantId { get; set; } = Constants.DefaultTenantId;

        /// <summary>
        /// User ID.
        /// </summary>
        public string? UserId { get; set; } = null;

        /// <summary>
        /// Credential ID.
        /// </summary>
        public string? CredentialId { get; set; } = null;

        /// <summary>
        /// Session ID.
        /// </summary>
        public string? SessionId { get; set; } = null;

        /// <summary>
        /// Principal type.
        /// </summary>
        public PrincipalTypeEnum PrincipalType { get; set; } = PrincipalTypeEnum.None;

        /// <summary>
        /// Auth scheme.
        /// </summary>
        public AuthSchemeEnum AuthScheme { get; set; } = AuthSchemeEnum.None;

        /// <summary>
        /// Principal ID.
        /// </summary>
        public string? PrincipalId
        {
            get
            {
                if (PrincipalType == PrincipalTypeEnum.Credential) return CredentialId;
                return UserId;
            }
        }

        /// <summary>
        /// Source IP address.
        /// </summary>
        public string? SourceIp { get; set; } = null;

        /// <summary>
        /// Whether the request is authenticated.
        /// </summary>
        public bool IsAuthenticated { get; set; } = false;
    }
}
