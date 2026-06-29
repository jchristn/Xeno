namespace Xeno.Sdk
{
    using System;

    /// <summary>
    /// Xeno token response DTO.
    /// </summary>
    public class TokenResponse
    {
        /// <summary>
        /// Gets or sets the token.
        /// </summary>
        public string Token { get; set; } = String.Empty;

        /// <summary>
        /// Gets or sets the token type.
        /// </summary>
        public string TokenType { get; set; } = String.Empty;

        /// <summary>
        /// Gets or sets the tenant id.
        /// </summary>
        public string TenantId { get; set; } = String.Empty;

        /// <summary>
        /// Gets or sets the expires utc.
        /// </summary>
        public DateTime? ExpiresUtc { get; set; }
    }
}
