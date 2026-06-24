namespace Xeno.Core.Models
{
    using System;

    /// <summary>
    /// Auth token issuance result.
    /// </summary>
    public class AuthTokenResult
    {
        /// <summary>
        /// Session token.
        /// </summary>
        public string Token { get; set; } = String.Empty;

        /// <summary>
        /// Token type.
        /// </summary>
        public string TokenType { get; set; } = "Bearer";

        /// <summary>
        /// Tenant ID.
        /// </summary>
        public string TenantId { get; set; } = Constants.DefaultTenantId;

        /// <summary>
        /// Session ID.
        /// </summary>
        public string SessionId { get; set; } = String.Empty;

        /// <summary>
        /// Expiration timestamp.
        /// </summary>
        public DateTime ExpiresUtc { get; set; } = DateTime.UtcNow.AddHours(1);
    }
}
