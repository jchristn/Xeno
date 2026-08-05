namespace Xeno.Core.Database.Interfaces
{
    using Xeno.Core.Enums;

    /// <summary>
    /// Filter and paging options for querying onboarding requests.
    /// </summary>
    public class OnboardingQuery
    {
        /// <summary>
        /// Status to match, or null for any status.
        /// </summary>
        public OnboardingStatusEnum? Status { get; set; } = null;

        /// <summary>
        /// Tenant identifier to match, or null for any tenant.
        /// </summary>
        public string? TenantId { get; set; } = null;

        /// <summary>
        /// Maximum rows to return. Zero or negative returns all matching rows.
        /// </summary>
        public int Limit { get; set; } = 0;

        /// <summary>
        /// Row offset for paging.
        /// </summary>
        public int Offset { get; set; } = 0;

        /// <summary>
        /// Order by creation time descending when true, ascending when false.
        /// </summary>
        public bool OrderDescending { get; set; } = true;

        /// <summary>
        /// Instantiate.
        /// </summary>
        public OnboardingQuery()
        {
        }
    }
}
