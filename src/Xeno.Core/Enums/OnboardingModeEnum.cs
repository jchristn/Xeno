namespace Xeno.Core.Enums
{
    /// <summary>
    /// Governs how the public onboarding sign-up endpoint behaves.
    /// </summary>
    public enum OnboardingModeEnum
    {
        /// <summary>
        /// Open self-service: sign-up immediately creates an active tenant and returns a token.
        /// Suitable for self-hosted deployments.
        /// </summary>
        Open,

        /// <summary>
        /// Gated cloud onboarding: sign-up creates an inactive tenant and user, requires email
        /// verification, and requires administrator approval before the account can be used.
        /// </summary>
        VerifyAndApprove,

        /// <summary>
        /// Sign-up is disabled; the onboarding endpoint rejects new tenant creation.
        /// </summary>
        Disabled
    }
}
