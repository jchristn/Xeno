namespace Xeno.Core.Database.Interfaces
{
    using System.Collections.Generic;
    using System.Threading;
    using System.Threading.Tasks;
    using Xeno.Core.Models;

    /// <summary>
    /// Model definition data access methods. Model definitions are tenant-owned and every query is
    /// scoped by tenant. The model definition's ordered <see cref="ModelDefinition.Labels"/> collection
    /// is persisted in the dedicated child table <c>model_definition_labels</c>.
    /// </summary>
    public interface IModelDefinitionMethods
    {
        /// <summary>
        /// Create a model definition and its label child rows. The model definition's
        /// <see cref="ModelDefinition.Id"/> is backfilled when the caller leaves it empty.
        /// </summary>
        /// <param name="definition">Model definition to create.</param>
        /// <param name="token">Cancellation token.</param>
        /// <returns>Created model definition.</returns>
        Task<ModelDefinition> CreateAsync(ModelDefinition definition, CancellationToken token = default);

        /// <summary>
        /// Read a model definition by identifier within a tenant, including its label child rows.
        /// </summary>
        /// <param name="tenantId">Tenant identifier.</param>
        /// <param name="id">Model definition identifier.</param>
        /// <param name="token">Cancellation token.</param>
        /// <returns>Model definition, or null when not found.</returns>
        Task<ModelDefinition?> ReadAsync(string tenantId, string id, CancellationToken token = default);

        /// <summary>
        /// Read a model definition by name within a tenant, including its label child rows.
        /// </summary>
        /// <param name="tenantId">Tenant identifier.</param>
        /// <param name="name">Model name.</param>
        /// <param name="token">Cancellation token.</param>
        /// <returns>Model definition, or null when not found.</returns>
        Task<ModelDefinition?> ReadByNameAsync(string tenantId, string name, CancellationToken token = default);

        /// <summary>
        /// Enumerate all model definitions for a tenant, including their label child rows.
        /// </summary>
        /// <param name="tenantId">Tenant identifier.</param>
        /// <param name="token">Cancellation token.</param>
        /// <returns>All model definitions for the tenant.</returns>
        Task<List<ModelDefinition>> EnumerateAsync(string tenantId, CancellationToken token = default);

        /// <summary>
        /// Update a model definition and replace its label child rows.
        /// </summary>
        /// <param name="definition">Model definition to update.</param>
        /// <param name="token">Cancellation token.</param>
        /// <returns>Updated model definition.</returns>
        Task<ModelDefinition> UpdateAsync(ModelDefinition definition, CancellationToken token = default);

        /// <summary>
        /// Delete a model definition and its label child rows within a tenant.
        /// </summary>
        /// <param name="tenantId">Tenant identifier.</param>
        /// <param name="id">Model definition identifier.</param>
        /// <param name="token">Cancellation token.</param>
        /// <returns>True when a row was deleted.</returns>
        Task<bool> DeleteAsync(string tenantId, string id, CancellationToken token = default);

        /// <summary>
        /// Delete every model definition owned by a tenant (and its label child rows) in a single set-based operation.
        /// </summary>
        /// <param name="tenantId">Tenant identifier.</param>
        /// <param name="token">Cancellation token.</param>
        /// <returns>Number of model definition rows deleted.</returns>
        Task<int> DeleteByTenantAsync(string tenantId, CancellationToken token = default);

        /// <summary>
        /// Determine whether a model definition exists within a tenant.
        /// </summary>
        /// <param name="tenantId">Tenant identifier.</param>
        /// <param name="id">Model definition identifier.</param>
        /// <param name="token">Cancellation token.</param>
        /// <returns>True when the model definition exists.</returns>
        Task<bool> ExistsAsync(string tenantId, string id, CancellationToken token = default);
    }
}
