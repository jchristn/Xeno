namespace Xeno.Core.Database.Interfaces
{
    using System.Collections.Generic;
    using System.Threading;
    using System.Threading.Tasks;
    using Xeno.Core.Models;

    /// <summary>
    /// Model configuration data access methods. Model configurations are tenant-owned and every query
    /// is scoped by tenant. The configuration's <see cref="ModelConfiguration.PinnedCompletionsProperties"/>
    /// map is persisted in the dedicated child table <c>model_configuration_pinned_properties</c>, keyed
    /// by property name. The child <c>value_json</c> column is the single, explicitly unmanaged JSON column
    /// permitted by the repository requirements: it stores each arbitrary property value as a serialized
    /// JSON document.
    /// </summary>
    public interface IModelConfigurationMethods
    {
        /// <summary>
        /// Create a model configuration and its pinned-property child rows. The configuration's
        /// <see cref="ModelConfiguration.Id"/> is backfilled when the caller leaves it empty.
        /// </summary>
        /// <param name="configuration">Model configuration to create.</param>
        /// <param name="token">Cancellation token.</param>
        /// <returns>Created model configuration.</returns>
        Task<ModelConfiguration> CreateAsync(ModelConfiguration configuration, CancellationToken token = default);

        /// <summary>
        /// Read a model configuration by identifier within a tenant, including its pinned-property child rows.
        /// </summary>
        /// <param name="tenantId">Tenant identifier.</param>
        /// <param name="id">Configuration identifier.</param>
        /// <param name="token">Cancellation token.</param>
        /// <returns>Model configuration, or null when not found.</returns>
        Task<ModelConfiguration?> ReadAsync(string tenantId, string id, CancellationToken token = default);

        /// <summary>
        /// Read a model configuration by name within a tenant, including its pinned-property child rows.
        /// </summary>
        /// <param name="tenantId">Tenant identifier.</param>
        /// <param name="name">Configuration name.</param>
        /// <param name="token">Cancellation token.</param>
        /// <returns>Model configuration, or null when not found.</returns>
        Task<ModelConfiguration?> ReadByNameAsync(string tenantId, string name, CancellationToken token = default);

        /// <summary>
        /// Enumerate all model configurations for a tenant, including their pinned-property child rows.
        /// </summary>
        /// <param name="tenantId">Tenant identifier.</param>
        /// <param name="token">Cancellation token.</param>
        /// <returns>All model configurations for the tenant.</returns>
        Task<List<ModelConfiguration>> EnumerateAsync(string tenantId, CancellationToken token = default);

        /// <summary>
        /// Update a model configuration and replace its pinned-property child rows.
        /// </summary>
        /// <param name="configuration">Model configuration to update.</param>
        /// <param name="token">Cancellation token.</param>
        /// <returns>Updated model configuration.</returns>
        Task<ModelConfiguration> UpdateAsync(ModelConfiguration configuration, CancellationToken token = default);

        /// <summary>
        /// Delete a model configuration and its pinned-property child rows within a tenant.
        /// </summary>
        /// <param name="tenantId">Tenant identifier.</param>
        /// <param name="id">Configuration identifier.</param>
        /// <param name="token">Cancellation token.</param>
        /// <returns>True when a row was deleted.</returns>
        Task<bool> DeleteAsync(string tenantId, string id, CancellationToken token = default);

        /// <summary>
        /// Determine whether a model configuration exists within a tenant.
        /// </summary>
        /// <param name="tenantId">Tenant identifier.</param>
        /// <param name="id">Configuration identifier.</param>
        /// <param name="token">Cancellation token.</param>
        /// <returns>True when the configuration exists.</returns>
        Task<bool> ExistsAsync(string tenantId, string id, CancellationToken token = default);
    }
}
