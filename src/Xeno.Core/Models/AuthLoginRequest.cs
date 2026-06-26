namespace Xeno.Core.Models
{
    using System;
    using System.Text.Json.Serialization;

    /// <summary>
    /// Authentication login request body.
    /// </summary>
    public class AuthLoginRequest
    {
        /// <summary>
        /// Tenant ID for the login attempt.
        /// </summary>
        [JsonPropertyName("tenantId")]
        public string? TenantId { get; set; } = null;

        /// <summary>
        /// Username supplied by the user.
        /// </summary>
        [JsonPropertyName("username")]
        public string? Username { get; set; } = null;

        /// <summary>
        /// Email address supplied as an alternate username.
        /// </summary>
        [JsonPropertyName("email")]
        public string? Email { get; set; } = null;

        /// <summary>
        /// Password supplied by the user.
        /// </summary>
        [JsonPropertyName("password")]
        public string? Password { get; set; } = null;
    }
}
