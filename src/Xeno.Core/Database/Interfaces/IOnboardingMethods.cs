namespace Xeno.Core.Database.Interfaces
{
    using System.Threading;
    using System.Threading.Tasks;
    using Xeno.Core.Models;

    /// <summary>
    /// Persisted cloud onboarding request methods. Onboarding requests live in their own table and
    /// are never cached in the in-memory platform state.
    /// </summary>
    public interface IOnboardingMethods
    {
        /// <summary>
        /// Insert an onboarding request.
        /// </summary>
        /// <param name="request">Request to persist.</param>
        /// <param name="token">Cancellation token.</param>
        /// <returns>Task.</returns>
        Task InsertAsync(OnboardingRequest request, CancellationToken token);

        /// <summary>
        /// Update an existing onboarding request in place.
        /// </summary>
        /// <param name="request">Request to update, matched by identifier.</param>
        /// <param name="token">Cancellation token.</param>
        /// <returns>Updated row count.</returns>
        Task<int> UpdateAsync(OnboardingRequest request, CancellationToken token);

        /// <summary>
        /// Read the onboarding request with the specified verification token.
        /// </summary>
        /// <param name="verificationToken">Verification token.</param>
        /// <param name="token">Cancellation token.</param>
        /// <returns>The request, or null when not found.</returns>
        Task<OnboardingRequest?> GetByTokenAsync(string verificationToken, CancellationToken token);

        /// <summary>
        /// Read the onboarding request for the specified tenant.
        /// </summary>
        /// <param name="tenantId">Tenant identifier.</param>
        /// <param name="token">Cancellation token.</param>
        /// <returns>The request, or null when not found.</returns>
        Task<OnboardingRequest?> GetByTenantAsync(string tenantId, CancellationToken token);

        /// <summary>
        /// Query onboarding requests with paging and total count.
        /// </summary>
        /// <param name="query">Filter and paging options.</param>
        /// <param name="token">Cancellation token.</param>
        /// <returns>Matching page and total count.</returns>
        Task<OnboardingRecordPage> QueryAsync(OnboardingQuery query, CancellationToken token);

        /// <summary>
        /// Count onboarding requests matching a filter.
        /// </summary>
        /// <param name="query">Filter options.</param>
        /// <param name="token">Cancellation token.</param>
        /// <returns>Matching count.</returns>
        Task<int> CountAsync(OnboardingQuery query, CancellationToken token);

        /// <summary>
        /// Delete onboarding requests for a tenant.
        /// </summary>
        /// <param name="tenantId">Tenant identifier.</param>
        /// <param name="token">Cancellation token.</param>
        /// <returns>Deleted row count.</returns>
        Task<int> DeleteByTenantAsync(string tenantId, CancellationToken token);
    }
}
