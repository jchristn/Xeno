namespace Xeno.Core.Models
{
    using System;
    using System.Text.Json.Serialization;

    /// <summary>
    /// Dashboard login response.
    /// </summary>
    public class AuthDashboardLoginResponse
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
        public string TokenType { get; set; } = "Bearer";

        /// <summary>
        /// Tenant ID.
        /// </summary>
        [JsonPropertyName("tenantId")]
        public string TenantId { get; set; } = Constants.DefaultTenantId;

        /// <summary>
        /// Tenant name.
        /// </summary>
        [JsonPropertyName("tenantName")]
        public string TenantName { get; set; } = "Default";

        /// <summary>
        /// User ID.
        /// </summary>
        [JsonPropertyName("userId")]
        public string UserId { get; set; } = String.Empty;

        /// <summary>
        /// User email address.
        /// </summary>
        [JsonPropertyName("email")]
        public string Email { get; set; } = String.Empty;

        /// <summary>
        /// Session ID.
        /// </summary>
        [JsonPropertyName("sessionId")]
        public string SessionId { get; set; } = String.Empty;

        /// <summary>
        /// Expiration timestamp.
        /// </summary>
        [JsonPropertyName("expiresUtc")]
        public DateTime ExpiresUtc { get; set; } = DateTime.UtcNow.AddHours(1);
    }
}
