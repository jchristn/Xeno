namespace Xeno.Sdk
{
    using System;
    using System.Text.Json.Serialization;

    /// <summary>
    /// Onboarding credential details.
    /// </summary>
    public class AuthOnboardingCredentialDetails
    {
        /// <summary>
        /// Credential identifier.
        /// </summary>
        [JsonPropertyName("id")]
        public string Id { get; set; } = String.Empty;

        /// <summary>
        /// Tenant identifier.
        /// </summary>
        [JsonPropertyName("tenantId")]
        public string TenantId { get; set; } = String.Empty;

        /// <summary>
        /// Owning user identifier.
        /// </summary>
        [JsonPropertyName("userId")]
        public string UserId { get; set; } = String.Empty;

        /// <summary>
        /// Access key.
        /// </summary>
        [JsonPropertyName("accessKey")]
        public string AccessKey { get; set; } = String.Empty;

        /// <summary>
        /// One-time-visible plaintext secret key.
        /// </summary>
        [JsonPropertyName("secretKey")]
        public string SecretKey { get; set; } = String.Empty;

        /// <summary>
        /// Whether the credential is active.
        /// </summary>
        [JsonPropertyName("active")]
        public bool Active { get; set; }
    }
}
