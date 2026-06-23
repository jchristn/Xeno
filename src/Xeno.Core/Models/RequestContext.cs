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
        /// Source IP address.
        /// </summary>
        public string? SourceIp { get; set; } = null;

        /// <summary>
        /// Whether the request is authenticated.
        /// </summary>
        public bool IsAuthenticated { get; set; } = false;
    }
}
