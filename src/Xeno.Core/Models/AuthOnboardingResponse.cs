namespace Xeno.Core.Models
{
    using System.Text.Json.Serialization;

    /// <summary>
    /// Public onboarding response.
    /// </summary>
    public class AuthOnboardingResponse
    {
        /// <summary>
        /// Operation status.
        /// </summary>
        [JsonPropertyName("status")]
        public string Status { get; set; } = "success";

        /// <summary>
        /// Tenant details.
        /// </summary>
        [JsonPropertyName("tenant")]
        public AuthOnboardingTenantDetails Tenant { get; set; } = new AuthOnboardingTenantDetails();

        /// <summary>
        /// User details.
        /// </summary>
        [JsonPropertyName("user")]
        public AuthOnboardingUserDetails User { get; set; } = new AuthOnboardingUserDetails();

        /// <summary>
        /// Credential details with the one-time-visible secret key.
        /// </summary>
        [JsonPropertyName("credential")]
        public AuthOnboardingCredentialDetails Credential { get; set; } = new AuthOnboardingCredentialDetails();

        /// <summary>
        /// Issued dashboard token details.
        /// </summary>
        [JsonPropertyName("token")]
        public AuthOnboardingTokenDetails Token { get; set; } = new AuthOnboardingTokenDetails();

        /// <summary>
        /// Request metadata useful for onboarding drill-down and telemetry.
        /// </summary>
        [JsonPropertyName("request")]
        public AuthOnboardingRequestMetadata Request { get; set; } = new AuthOnboardingRequestMetadata();
    }
}
