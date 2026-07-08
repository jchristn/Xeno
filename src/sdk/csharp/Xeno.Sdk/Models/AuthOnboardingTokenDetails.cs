namespace Xeno.Sdk
{
    using System;
    using System.Text.Json.Serialization;

    /// <summary>
    /// Onboarding token details.
    /// </summary>
    public class AuthOnboardingTokenDetails
    {
        /// <summary>
        /// Session token.
        /// </summary>
        [JsonPropertyName("token")]
        public string Token { get; set; } = String.Empty;

        /// <summary>
        /// Token type.
        /// </summary>
        [JsonPropertyName("tokenType")]
        public string TokenType { get; set; } = String.Empty;

        /// <summary>
        /// Tenant identifier.
        /// </summary>
        [JsonPropertyName("tenantId")]
        public string TenantId { get; set; } = String.Empty;

        /// <summary>
        /// User identifier.
        /// </summary>
        [JsonPropertyName("userId")]
        public string UserId { get; set; } = String.Empty;

        /// <summary>
        /// Session identifier.
        /// </summary>
        [JsonPropertyName("sessionId")]
        public string SessionId { get; set; } = String.Empty;

        /// <summary>
        /// Expiration timestamp.
        /// </summary>
        [JsonPropertyName("expiresUtc")]
        public DateTime? ExpiresUtc { get; set; }
    }
}
