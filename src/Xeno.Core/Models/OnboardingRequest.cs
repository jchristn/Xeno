namespace Xeno.Core.Models
{
    using System;
    using Xeno.Core.Enums;
    using Xeno.Core.Helpers;

    /// <summary>
    /// A cloud onboarding request captured when a prospective organization signs up. Stored in the
    /// dedicated onboarding table, keyed by a random verification token, and never cached in the
    /// in-memory platform state.
    /// </summary>
    public class OnboardingRequest
    {
        /// <summary>
        /// Onboarding request ID.
        /// </summary>
        public string Id { get; set; } = IdGenerator.NewId("onb_");

        /// <summary>
        /// Created tenant ID.
        /// </summary>
        public string TenantId { get; set; } = string.Empty;

        /// <summary>
        /// Created administrator user ID.
        /// </summary>
        public string UserId { get; set; } = string.Empty;

        /// <summary>
        /// Organization (tenant) name.
        /// </summary>
        public string Organization { get; set; } = string.Empty;

        /// <summary>
        /// Contact person's name.
        /// </summary>
        public string ContactName { get; set; } = string.Empty;

        /// <summary>
        /// Contact email address.
        /// </summary>
        public string Email { get; set; } = string.Empty;

        /// <summary>
        /// Source IP address at sign-up.
        /// </summary>
        public string? CreationIpAddress { get; set; } = null;

        /// <summary>
        /// Random email-verification token.
        /// </summary>
        public string Token { get; set; } = string.Empty;

        /// <summary>
        /// Request status.
        /// </summary>
        public OnboardingStatusEnum Status { get; set; } = OnboardingStatusEnum.PendingVerification;

        /// <summary>
        /// Created timestamp.
        /// </summary>
        public DateTime CreatedUtc { get; set; } = DateTime.UtcNow;

        /// <summary>
        /// Email verification timestamp, when verified.
        /// </summary>
        public DateTime? VerifiedUtc { get; set; } = null;

        /// <summary>
        /// Approval timestamp, when approved.
        /// </summary>
        public DateTime? ApprovedUtc { get; set; } = null;

        /// <summary>
        /// Instantiate.
        /// </summary>
        public OnboardingRequest()
        {
        }
    }
}
