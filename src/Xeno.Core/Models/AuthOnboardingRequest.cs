namespace Xeno.Core.Models
{
    using System.Text.Json.Serialization;

    /// <summary>
    /// Public onboarding request body for creating the first tenant administrator.
    /// </summary>
    public class AuthOnboardingRequest
    {
        /// <summary>
        /// Optional tenant ID. Generated when omitted.
        /// </summary>
        [JsonPropertyName("tenantId")]
        public string? TenantId { get; set; } = null;

        /// <summary>
        /// Tenant display name.
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

        /// <summary>
        /// Optional initial credential access key. Generated when omitted.
        /// </summary>
        [JsonPropertyName("accessKey")]
        public string? AccessKey { get; set; } = null;
    }
}
