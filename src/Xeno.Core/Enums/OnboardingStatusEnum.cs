namespace Xeno.Core.Enums
{
    /// <summary>
    /// Lifecycle status of a cloud onboarding request.
    /// </summary>
    public enum OnboardingStatusEnum
    {
        /// <summary>
        /// Sign-up submitted; awaiting email verification.
        /// </summary>
        PendingVerification,

        /// <summary>
        /// Email verified; awaiting administrator approval.
        /// </summary>
        Verified,

        /// <summary>
        /// Approved by an administrator; tenant and user are active.
        /// </summary>
        Approved,

        /// <summary>
        /// Rejected by an administrator.
        /// </summary>
        Rejected
    }
}
