namespace Xeno.Core.Models
{
    using System;
    using Xeno.Core.Enums;
    using Xeno.Core.Helpers;

    /// <summary>
    /// Authenticated session.
    /// </summary>
    public class AuthSession
    {
        /// <summary>
        /// Session ID.
        /// </summary>
        public string Id { get; set; } = IdGenerator.NewId("sess_");

        /// <summary>
        /// Tenant ID.
        /// </summary>
        public string TenantId { get; set; } = Constants.DefaultTenantId;

        /// <summary>
        /// Principal type.
        /// </summary>
        public PrincipalTypeEnum PrincipalType { get; set; } = PrincipalTypeEnum.User;

        /// <summary>
        /// User ID.
        /// </summary>
        public string? UserId { get; set; } = null;

        /// <summary>
        /// Credential ID.
        /// </summary>
        public string? CredentialId { get; set; } = null;

        /// <summary>
        /// Token hash.
        /// </summary>
        public string TokenHash { get; set; } = String.Empty;

        /// <summary>
        /// Authentication scheme that created the session.
        /// </summary>
        public AuthSchemeEnum AuthScheme { get; set; } = AuthSchemeEnum.Credential;

        /// <summary>
        /// Created timestamp.
        /// </summary>
        public DateTime CreatedUtc { get; set; } = DateTime.UtcNow;

        /// <summary>
        /// Last update timestamp.
        /// </summary>
        public DateTime LastUpdateUtc { get; set; } = DateTime.UtcNow;

        /// <summary>
        /// Expiration timestamp.
        /// </summary>
        public DateTime ExpiresUtc { get; set; } = DateTime.UtcNow.AddHours(1);

        /// <summary>
        /// Whether the session is active.
        /// </summary>
        public bool Active { get; set; } = true;
    }
}
