namespace Xeno.Core.Database
{
    using System;
    using System.Collections.Generic;
    using System.Threading;
    using System.Threading.Tasks;
    using Xeno.Core.Models;
    using Xeno.Core.Services;

    /// <summary>
    /// First-boot seeder for the normalized relational store. Persists the live in-memory control-plane
    /// graph (the same <see cref="XenoPlatform"/> the driver just loaded or seeded) into the per-entity
    /// repositories, so the relational store shares identifiers with the legacy state during the
    /// migration. Idempotent: it is a no-op once any tenant row exists.
    /// </summary>
    public static class DatabaseSeeder
    {
        /// <summary>
        /// Seed the tenants, users, credentials, permissions, roles, role assignments, clients, model
        /// definitions, model configurations, and routing policies from the supplied platform state into
        /// the relational tables when the tenant table is empty. Because it copies the already-loaded
        /// state, it also serves as a one-time migration from the legacy serialized state blob.
        /// </summary>
        /// <param name="driver">Initialized database driver whose repositories are constructed.</param>
        /// <param name="platform">Live platform state to persist.</param>
        /// <param name="token">Cancellation token.</param>
        /// <returns>True when seed rows were written; false when seeding was skipped.</returns>
        public static async Task<bool> SeedIfEmptyAsync(DatabaseDriverBase driver, XenoPlatform platform, CancellationToken token = default)
        {
            if (driver == null || platform == null) return false;
            if (driver.Tenants == null || driver.Users == null || driver.Credentials == null
                || driver.Permissions == null || driver.Roles == null || driver.RoleAssignments == null
                || driver.Clients == null || driver.ModelDefinitions == null || driver.ModelConfigurations == null
                || driver.RoutingPolicies == null)
            {
                return false;
            }

            List<Tenant> existing = await driver.Tenants.EnumerateAsync(token).ConfigureAwait(false);
            if (existing.Count > 0) return false;

            foreach (Tenant tenant in platform.Tenants)
            {
                await driver.Tenants.CreateAsync(tenant, token).ConfigureAwait(false);
            }

            foreach (User user in platform.Users)
            {
                await driver.Users.CreateAsync(user, token).ConfigureAwait(false);
            }

            foreach (Credential credential in platform.Credentials)
            {
                await driver.Credentials.CreateAsync(credential, token).ConfigureAwait(false);
            }

            foreach (Permission permission in platform.Permissions)
            {
                await driver.Permissions.CreateAsync(permission, token).ConfigureAwait(false);
            }

            foreach (Role role in platform.Roles)
            {
                await driver.Roles.CreateAsync(role, token).ConfigureAwait(false);
            }

            foreach (RoleAssignment assignment in platform.RoleAssignments)
            {
                await driver.RoleAssignments.CreateAsync(assignment, token).ConfigureAwait(false);
            }

            foreach (XenoClientRecord client in platform.Clients)
            {
                await driver.Clients.CreateAsync(client, token).ConfigureAwait(false);
            }

            foreach (ModelDefinition definition in platform.ModelDefinitions)
            {
                await driver.ModelDefinitions.CreateAsync(definition, token).ConfigureAwait(false);
            }

            foreach (ModelConfiguration configuration in platform.ModelConfigurations)
            {
                await driver.ModelConfigurations.CreateAsync(configuration, token).ConfigureAwait(false);
            }

            foreach (RoutingPolicy policy in platform.RoutingPolicies)
            {
                await driver.RoutingPolicies.CreateAsync(policy, token).ConfigureAwait(false);
            }

            return true;
        }

        /// <summary>
        /// Load the entire control plane from the relational tables into an in-memory
        /// <see cref="XenoPlatform"/> snapshot. Used only by the rare whole-state administrative
        /// operations (platform export/import and tenant nuke) where the in-memory graph shape is the
        /// simplest expression of the operation; normal request paths read the repositories directly.
        /// </summary>
        /// <param name="driver">Initialized database driver whose repositories are constructed.</param>
        /// <param name="token">Cancellation token.</param>
        /// <returns>Populated platform snapshot.</returns>
        public static async Task<XenoPlatform> LoadSnapshotAsync(DatabaseDriverBase driver, CancellationToken token = default)
        {
            if (driver == null) throw new ArgumentNullException(nameof(driver));
            XenoPlatform platform = new XenoPlatform();
            if (driver.Tenants == null) return platform;

            List<Tenant> tenants = await driver.Tenants.EnumerateAsync(token).ConfigureAwait(false);
            platform.Tenants.AddRange(tenants);

            if (driver.LedgerTenantLinks != null) platform.LedgerTenantLinks.AddRange(await driver.LedgerTenantLinks.EnumerateAsync(token).ConfigureAwait(false));

            foreach (Tenant tenant in tenants)
            {
                string tenantId = tenant.Id;
                if (driver.Users != null) platform.Users.AddRange(await driver.Users.EnumerateAsync(tenantId, token).ConfigureAwait(false));
                if (driver.Credentials != null) platform.Credentials.AddRange(await driver.Credentials.EnumerateAsync(tenantId, token).ConfigureAwait(false));
                if (driver.AuthSessions != null) platform.AuthSessions.AddRange(await driver.AuthSessions.EnumerateByTenantAsync(tenantId, token).ConfigureAwait(false));
                if (driver.Permissions != null) platform.Permissions.AddRange(await driver.Permissions.EnumerateAsync(tenantId, token).ConfigureAwait(false));
                if (driver.Roles != null) platform.Roles.AddRange(await driver.Roles.EnumerateAsync(tenantId, token).ConfigureAwait(false));
                if (driver.RoleAssignments != null) platform.RoleAssignments.AddRange(await driver.RoleAssignments.EnumerateAsync(tenantId, token).ConfigureAwait(false));
                if (driver.Clients != null) platform.Clients.AddRange(await driver.Clients.EnumerateAsync(tenantId, token).ConfigureAwait(false));
                if (driver.ModelDefinitions != null) platform.ModelDefinitions.AddRange(await driver.ModelDefinitions.EnumerateAsync(tenantId, token).ConfigureAwait(false));
                if (driver.ModelConfigurations != null) platform.ModelConfigurations.AddRange(await driver.ModelConfigurations.EnumerateAsync(tenantId, token).ConfigureAwait(false));
                if (driver.RoutingPolicies != null) platform.RoutingPolicies.AddRange(await driver.RoutingPolicies.EnumerateAsync(tenantId, token).ConfigureAwait(false));
                if (driver.LedgerAccountLinks != null) platform.LedgerAccountLinks.AddRange(await driver.LedgerAccountLinks.EnumerateByTenantAsync(tenantId, token).ConfigureAwait(false));
            }

            return platform;
        }

        /// <summary>
        /// Delete every control-plane row owned by a tenant across all normalized entity tables
        /// (users, credentials, auth sessions, roles, permissions, role assignments, clients, model
        /// definitions, model configurations, routing policies, ledger links, and the tenant row itself).
        /// Request history, audit, and telemetry rows are purged separately by the driver. Used by the
        /// tenant-nuke administrative operation.
        /// </summary>
        /// <param name="driver">Initialized database driver whose repositories are constructed.</param>
        /// <param name="tenantId">Tenant identifier to purge.</param>
        /// <param name="includeTenantRow">Whether to delete the tenant row itself in addition to its owned rows.</param>
        /// <param name="token">Cancellation token.</param>
        /// <returns>Task.</returns>
        public static async Task PurgeTenantEntitiesAsync(DatabaseDriverBase driver, string tenantId, bool includeTenantRow, CancellationToken token = default)
        {
            if (driver == null) throw new ArgumentNullException(nameof(driver));
            if (String.IsNullOrWhiteSpace(tenantId)) throw new ArgumentException("Tenant ID is required.", nameof(tenantId));

            // Set-based tenant purge: each repository issues a single DELETE (child rows removed in the same
            // statement/transaction by the repository) rather than enumerating and deleting row by row.
            // onboarding_requests is a leaf table, deleted first so ordering never depends on other tables.
            if (driver.Onboarding != null) await driver.Onboarding.DeleteByTenantAsync(tenantId, token).ConfigureAwait(false);
            if (driver.AuthSessions != null) await driver.AuthSessions.DeleteByTenantAsync(tenantId, token).ConfigureAwait(false);
            if (driver.RoleAssignments != null) await driver.RoleAssignments.DeleteByTenantAsync(tenantId, token).ConfigureAwait(false);
            if (driver.Roles != null) await driver.Roles.DeleteByTenantAsync(tenantId, token).ConfigureAwait(false);
            if (driver.Permissions != null) await driver.Permissions.DeleteByTenantAsync(tenantId, token).ConfigureAwait(false);
            if (driver.Credentials != null) await driver.Credentials.DeleteByTenantAsync(tenantId, token).ConfigureAwait(false);
            if (driver.Users != null) await driver.Users.DeleteByTenantAsync(tenantId, token).ConfigureAwait(false);
            if (driver.Clients != null) await driver.Clients.DeleteByTenantAsync(tenantId, token).ConfigureAwait(false);
            if (driver.ModelDefinitions != null) await driver.ModelDefinitions.DeleteByTenantAsync(tenantId, token).ConfigureAwait(false);
            if (driver.ModelConfigurations != null) await driver.ModelConfigurations.DeleteByTenantAsync(tenantId, token).ConfigureAwait(false);
            if (driver.RoutingPolicies != null) await driver.RoutingPolicies.DeleteByTenantAsync(tenantId, token).ConfigureAwait(false);
            if (driver.LedgerAccountLinks != null) await driver.LedgerAccountLinks.DeleteByTenantAsync(tenantId, token).ConfigureAwait(false);
            if (driver.LedgerTenantLinks != null) await driver.LedgerTenantLinks.DeleteByTenantAsync(tenantId, token).ConfigureAwait(false);

            if (includeTenantRow && driver.Tenants != null) await driver.Tenants.DeleteAsync(tenantId, token).ConfigureAwait(false);
        }

        /// <summary>
        /// Ensure the built-in security defaults (permissions, roles, role assignments, default routing
        /// policy, and default credential) exist for a single tenant, persisting them relationally. Loads
        /// the tenant's current control-plane rows, runs the in-memory reconciler
        /// (<see cref="XenoPlatform.EnsureTenantDefaults(string)"/>), then upserts the reconciled graph
        /// back through the repositories. Idempotent because every repository create is an upsert.
        /// </summary>
        /// <param name="driver">Initialized database driver whose repositories are constructed.</param>
        /// <param name="tenantId">Tenant identifier to reconcile.</param>
        /// <param name="token">Cancellation token.</param>
        /// <returns>Task.</returns>
        public static async Task SeedTenantDefaultsAsync(DatabaseDriverBase driver, string tenantId, CancellationToken token = default)
        {
            if (driver == null) throw new ArgumentNullException(nameof(driver));
            if (String.IsNullOrWhiteSpace(tenantId)) throw new ArgumentException("Tenant ID is required.", nameof(tenantId));
            if (driver.Tenants == null || driver.Users == null || driver.Credentials == null
                || driver.Permissions == null || driver.Roles == null || driver.RoleAssignments == null
                || driver.RoutingPolicies == null)
            {
                return;
            }

            Tenant? tenant = await driver.Tenants.ReadAsync(tenantId, token).ConfigureAwait(false);
            if (tenant == null) return;

            XenoPlatform platform = new XenoPlatform();
            platform.Tenants.Add(tenant);
            platform.Users.AddRange(await driver.Users.EnumerateAsync(tenantId, token).ConfigureAwait(false));
            platform.Credentials.AddRange(await driver.Credentials.EnumerateAsync(tenantId, token).ConfigureAwait(false));
            platform.Permissions.AddRange(await driver.Permissions.EnumerateAsync(tenantId, token).ConfigureAwait(false));
            platform.Roles.AddRange(await driver.Roles.EnumerateAsync(tenantId, token).ConfigureAwait(false));
            platform.RoleAssignments.AddRange(await driver.RoleAssignments.EnumerateAsync(tenantId, token).ConfigureAwait(false));
            platform.RoutingPolicies.AddRange(await driver.RoutingPolicies.EnumerateAsync(tenantId, token).ConfigureAwait(false));

            platform.EnsureTenantDefaults(tenantId);

            foreach (Permission permission in platform.Permissions)
            {
                if (String.Equals(permission.TenantId, tenantId, StringComparison.OrdinalIgnoreCase))
                    await driver.Permissions.CreateAsync(permission, token).ConfigureAwait(false);
            }

            foreach (Role role in platform.Roles)
            {
                if (String.Equals(role.TenantId, tenantId, StringComparison.OrdinalIgnoreCase))
                    await driver.Roles.CreateAsync(role, token).ConfigureAwait(false);
            }

            foreach (Credential credential in platform.Credentials)
            {
                if (String.Equals(credential.TenantId, tenantId, StringComparison.OrdinalIgnoreCase))
                    await driver.Credentials.CreateAsync(credential, token).ConfigureAwait(false);
            }

            foreach (RoleAssignment assignment in platform.RoleAssignments)
            {
                if (String.Equals(assignment.TenantId, tenantId, StringComparison.OrdinalIgnoreCase))
                    await driver.RoleAssignments.CreateAsync(assignment, token).ConfigureAwait(false);
            }

            foreach (RoutingPolicy policy in platform.RoutingPolicies)
            {
                if (String.Equals(policy.TenantId, tenantId, StringComparison.OrdinalIgnoreCase))
                    await driver.RoutingPolicies.CreateAsync(policy, token).ConfigureAwait(false);
            }
        }
    }
}
