namespace Xeno.Sdk
{
    using System.Text.Json.Serialization;

    /// <summary>
    /// Onboarding request for creating a tenant administrator or adding a user to an existing tenant.
    /// </summary>
    public class AuthOnboardingRequest
    {
        /// <summary>
        /// Optional existing tenant ID. Omit to create a new tenant.
        /// </summary>
        [JsonPropertyName("tenantId")]
        public string? TenantId { get; set; } = null;

        /// <summary>
        /// Tenant display name. Required when <see cref="TenantId" /> is omitted.
        /// </summary>
        [JsonPropertyName("tenantName")]
        public string? TenantName { get; set; } = null;

        /// <summary>
        /// User email address.
        /// </summary>
        [JsonPropertyName("email")]
        public string? Email { get; set; } = null;

        /// <summary>
        /// Optional username. Defaults to email.
        /// </summary>
        [JsonPropertyName("username")]
        public string? Username { get; set; } = null;

        /// <summary>
        /// Initial user password.
        /// </summary>
        [JsonPropertyName("password")]
        public string? Password { get; set; } = null;
    }
}
