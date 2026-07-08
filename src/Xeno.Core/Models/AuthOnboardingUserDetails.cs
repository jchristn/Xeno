namespace Xeno.Core.Models
{
    using System;
    using System.Text.Json.Serialization;

    /// <summary>
    /// Onboarding user details.
    /// </summary>
    public class AuthOnboardingUserDetails
    {
        /// <summary>
        /// User identifier.
        /// </summary>
        [JsonPropertyName("id")]
        public string Id { get; set; } = String.Empty;

        /// <summary>
        /// Tenant identifier.
        /// </summary>
        [JsonPropertyName("tenantId")]
        public string TenantId { get; set; } = String.Empty;

        /// <summary>
        /// Username.
        /// </summary>
        [JsonPropertyName("username")]
        public string Username { get; set; } = String.Empty;

        /// <summary>
        /// Email address.
        /// </summary>
        [JsonPropertyName("email")]
        public string Email { get; set; } = String.Empty;

        /// <summary>
        /// Whether the user is a global administrator.
        /// </summary>
        [JsonPropertyName("isAdmin")]
        public bool IsAdmin { get; set; } = false;

        /// <summary>
        /// Whether the user is a tenant administrator.
        /// </summary>
        [JsonPropertyName("isTenantAdmin")]
        public bool IsTenantAdmin { get; set; } = false;

        /// <summary>
        /// Whether the user is active.
        /// </summary>
        [JsonPropertyName("active")]
        public bool Active { get; set; } = true;
    }
}
