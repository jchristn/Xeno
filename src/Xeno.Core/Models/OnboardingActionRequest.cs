namespace Xeno.Core.Models
{
    /// <summary>
    /// Request body for onboarding verify and approve actions.
    /// </summary>
    public class OnboardingActionRequest
    {
        /// <summary>
        /// Email verification token (verify action).
        /// </summary>
        public string? Token { get; set; } = null;

        /// <summary>
        /// Tenant identifier (approve action).
        /// </summary>
        public string? TenantId { get; set; } = null;

        /// <summary>
        /// Instantiate.
        /// </summary>
        public OnboardingActionRequest()
        {
        }
    }
}
