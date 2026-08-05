namespace Xeno.Email.Templates
{
    /// <summary>
    /// Context for the onboarding email verification template.
    /// </summary>
    public class OnboardingVerificationEmailContext
    {
        /// <summary>
        /// Organization (tenant) name.
        /// </summary>
        public string Organization { get; set; } = string.Empty;

        /// <summary>
        /// Contact person's name.
        /// </summary>
        public string ContactName { get; set; } = string.Empty;

        /// <summary>
        /// Absolute verification URL the recipient must click.
        /// </summary>
        public string VerificationUrl { get; set; } = string.Empty;

        /// <summary>
        /// Instantiate.
        /// </summary>
        public OnboardingVerificationEmailContext()
        {
        }
    }
}
