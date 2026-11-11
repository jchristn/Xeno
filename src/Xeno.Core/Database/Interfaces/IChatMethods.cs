namespace Xeno.Core.Database.Interfaces
{
    using System.Threading;
    using System.Threading.Tasks;
    using Xeno.Core.Chat;

    /// <summary>
    /// Server chat storage: conversations, messages, feedback, and chat turns per tenant and user.
    /// </summary>
    public interface IChatMethods
    {
        /// <summary>
        /// A chat store scoped to one user, or to every user in a tenant for review.
        /// </summary>
        /// <param name="tenantId">Tenant id.</param>
        /// <param name="userId">User id, or null for every user in the tenant (read-only use).</param>
        /// <returns>Scoped store.</returns>
        IChatStore For(string tenantId, string? userId);

        /// <summary>
        /// Delete every chat row in a tenant.
        /// </summary>
        /// <param name="tenantId">Tenant id.</param>
        /// <param name="token">Cancellation token.</param>
        /// <returns>Task.</returns>
        Task DeleteByTenantAsync(string tenantId, CancellationToken token);
    }
}
