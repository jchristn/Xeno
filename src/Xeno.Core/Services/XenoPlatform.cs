namespace Xeno.Core.Services
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Threading;
    using System.Threading.Tasks;
    using Xeno.Core.Enums;
    using Xeno.Core.Models;
    using Xeno.Core.Settings;

    /// <summary>
    /// Tenant-aware platform state and domain operations.
    /// </summary>
    public class XenoPlatform
    {
        /// <summary>
        /// Tenants.
        /// </summary>
        public List<Tenant> Tenants { get; } = new List<Tenant>();

        /// <summary>
        /// Users.
        /// </summary>
        public List<User> Users { get; } = new List<User>();

        /// <summary>
        /// Credentials.
        /// </summary>
        public List<Credential> Credentials { get; } = new List<Credential>();

        /// <summary>
        /// Auth sessions.
        /// </summary>
        public List<AuthSession> AuthSessions { get; } = new List<AuthSession>();

        /// <summary>
        /// Roles.
        /// </summary>
        public List<Role> Roles { get; } = new List<Role>();

        /// <summary>
        /// Permissions.
        /// </summary>
        public List<Permission> Permissions { get; } = new List<Permission>();

        /// <summary>
        /// Role assignments.
        /// </summary>
        public List<RoleAssignment> RoleAssignments { get; } = new List<RoleAssignment>();

        /// <summary>
        /// Clients.
        /// </summary>
        public List<XenoClientRecord> Clients { get; } = new List<XenoClientRecord>();

        /// <summary>
        /// Model definitions.
        /// </summary>
        public List<ModelDefinition> ModelDefinitions { get; } = new List<ModelDefinition>();

        /// <summary>
        /// Model configurations.
        /// </summary>
        public List<ModelConfiguration> ModelConfigurations { get; } = new List<ModelConfiguration>();

        /// <summary>
        /// Routing policies.
        /// </summary>
        public List<RoutingPolicy> RoutingPolicies { get; } = new List<RoutingPolicy>();

        /// <summary>
        /// Ledger tenant links.
        /// </summary>
        public List<LedgerTenantLink> LedgerTenantLinks { get; } = new List<LedgerTenantLink>();

        /// <summary>
        /// Ledger account links.
        /// </summary>
        public List<LedgerAccountLink> LedgerAccountLinks { get; } = new List<LedgerAccountLink>();

        /// <summary>
        /// Initialize seeded state.
        /// </summary>
        /// <param name="token">Cancellation token.</param>
        /// <returns>Task.</returns>
        public Task InitializeAsync(CancellationToken token = default)
        {
            token.ThrowIfCancellationRequested();
            if (Tenants.Count > 0) return Task.CompletedTask;

            Tenant tenant = new Tenant();
            Tenants.Add(tenant);

            User user = new User
            {
                TenantId = tenant.Id,
                Username = "admin",
                Email = "admin@xeno",
                IsAdmin = true,
                IsTenantAdmin = true,
                Active = true
            };
            AuthenticationService authentication = new AuthenticationService();
            authentication.SetUserPassword(user, "password");
            Users.Add(user);

            Credential credential = new Credential
            {
                TenantId = tenant.Id,
                UserId = user.Id,
                AccessKey = "default",
                SecretKey = "default",
                Active = true
            };
            Credentials.Add(credential);

            Permission readPermission = new Permission
            {
                TenantId = tenant.Id,
                ResourceType = "*",
                OperationType = Xeno.Core.Enums.OperationTypeEnum.Read,
                Permit = true,
                BuiltIn = true
            };
            Permissions.Add(readPermission);

            Permission writePermission = new Permission
            {
                TenantId = tenant.Id,
                ResourceType = "*",
                OperationType = Xeno.Core.Enums.OperationTypeEnum.Write,
                Permit = true,
                BuiltIn = true
            };
            Permissions.Add(writePermission);

            Permission deletePermission = new Permission
            {
                TenantId = tenant.Id,
                ResourceType = "*",
                OperationType = Xeno.Core.Enums.OperationTypeEnum.Delete,
                Permit = true,
                BuiltIn = true
            };
            Permissions.Add(deletePermission);

            Permission executePermission = new Permission
            {
                TenantId = tenant.Id,
                ResourceType = "*",
                OperationType = Xeno.Core.Enums.OperationTypeEnum.Execute,
                Permit = true,
                BuiltIn = true
            };
            Permissions.Add(executePermission);

            Permission inferenceReadPermission = new Permission
            {
                TenantId = tenant.Id,
                ResourceType = "Inference",
                OperationType = Xeno.Core.Enums.OperationTypeEnum.Read,
                Permit = true,
                BuiltIn = true
            };
            Permissions.Add(inferenceReadPermission);

            Permission inferenceExecutePermission = new Permission
            {
                TenantId = tenant.Id,
                ResourceType = "Inference",
                OperationType = Xeno.Core.Enums.OperationTypeEnum.Execute,
                Permit = true,
                BuiltIn = true
            };
            Permissions.Add(inferenceExecutePermission);

            Permission adminPermission = new Permission
            {
                TenantId = tenant.Id,
                ResourceType = "Admin",
                OperationType = Xeno.Core.Enums.OperationTypeEnum.Admin,
                Permit = true,
                BuiltIn = true
            };
            Permissions.Add(adminPermission);

            Role tenantAdmin = new Role
            {
                TenantId = tenant.Id,
                Name = "TenantAdmin",
                BuiltIn = true,
                PermissionIds = new List<string> { readPermission.Id, writePermission.Id, deletePermission.Id, executePermission.Id, inferenceReadPermission.Id, inferenceExecutePermission.Id, adminPermission.Id }
            };
            Roles.Add(tenantAdmin);

            Role viewer = new Role
            {
                TenantId = tenant.Id,
                Name = "Viewer",
                BuiltIn = true,
                PermissionIds = new List<string> { readPermission.Id }
            };
            Roles.Add(viewer);

            Role tenantMember = new Role
            {
                TenantId = tenant.Id,
                Name = "TenantMember",
                BuiltIn = true,
                PermissionIds = new List<string> { readPermission.Id }
            };
            Roles.Add(tenantMember);

            RoleAssignments.Add(new RoleAssignment
            {
                TenantId = tenant.Id,
                RoleId = tenantAdmin.Id,
                PrincipalType = Xeno.Core.Enums.PrincipalTypeEnum.User,
                PrincipalId = user.Id
            });

            XenoClientRecord client = new XenoClientRecord
            {
                TenantId = tenant.Id,
                Name = "local-client",
                Connected = false,
                Models = new List<XenoClientModel>
                {
                    new XenoClientModel
                    {
                        Name = "llama3.2:latest",
                        Provider = Xeno.Core.Enums.ApiProviderTypeEnum.Ollama,
                        Source = "local-ollama",
                        SupportsCompletions = true,
                        SupportsEmbeddings = false
                    },
                    new XenoClientModel
                    {
                        Name = "text-embedding-3-small",
                        Provider = Xeno.Core.Enums.ApiProviderTypeEnum.Ollama,
                        Source = "local-ollama",
                        SupportsCompletions = false,
                        SupportsEmbeddings = true
                    }
                }
            };
            Clients.Add(client);

            ModelDefinition chatModel = new ModelDefinition
            {
                TenantId = tenant.Id,
                Name = "llama3.2:latest",
                SupportsCompletions = true,
                SupportsEmbeddings = false,
                Labels = new List<string> { "chat" }
            };
            ModelDefinitions.Add(chatModel);

            ModelDefinition embeddingModel = new ModelDefinition
            {
                TenantId = tenant.Id,
                Name = "text-embedding-3-small",
                SupportsCompletions = false,
                SupportsEmbeddings = true,
                Labels = new List<string> { "embedding" }
            };
            ModelDefinitions.Add(embeddingModel);

            ModelConfiguration modelConfiguration = new ModelConfiguration
            {
                TenantId = tenant.Id,
                Name = "Default",
                Model = chatModel.Name
            };
            ModelConfigurations.Add(modelConfiguration);

            EnsureTenantDefaults(tenant.Id);

            return Task.CompletedTask;
        }

        /// <summary>
        /// Ensure first-boot and migration defaults exist for every tenant.
        /// </summary>
        public void EnsureTenantDefaults()
        {
            foreach (Tenant tenant in Tenants)
            {
                EnsureTenantDefaults(tenant.Id);
            }
        }

        /// <summary>
        /// Ensure first-boot and migration defaults exist for a tenant.
        /// </summary>
        /// <param name="tenantId">Tenant ID.</param>
        public void EnsureTenantDefaults(string tenantId)
        {
            if (String.IsNullOrWhiteSpace(tenantId)) throw new ArgumentException("Tenant ID is required.", nameof(tenantId));
            EnsureDefaultTenantSecurity(tenantId);
            EnsureDefaultRoutingPolicy(tenantId);
        }

        /// <summary>
        /// Remove all tenant-scoped records for a deleted tenant.
        /// </summary>
        /// <param name="tenantId">Tenant ID.</param>
        /// <returns>Removed record count.</returns>
        public int RemoveTenantScopedRecords(string tenantId)
        {
            if (String.IsNullOrWhiteSpace(tenantId)) throw new ArgumentException("Tenant ID is required.", nameof(tenantId));

            AdminNukeCounts counts = RemoveTenantScopedRecords(tenantId, true, true);
            int removed = counts.Users;
            removed += counts.Credentials;
            removed += counts.AuthSessions;
            removed += counts.Roles;
            removed += counts.Permissions;
            removed += counts.RoleAssignments;
            removed += counts.AuditRecords;
            removed += counts.Clients;
            removed += counts.ModelDefinitions;
            removed += counts.ModelConfigurations;
            removed += counts.RoutingPolicies;
            removed += counts.RequestHistory;
            return removed;
        }

        /// <summary>
        /// Count tenant-scoped records for a nuke operation.
        /// </summary>
        /// <param name="tenantId">Tenant ID.</param>
        /// <param name="includeAuditRecords">Whether audit records are included.</param>
        /// <param name="includeRequestHistory">Whether request history is included.</param>
        /// <returns>Object counts.</returns>
        public AdminNukeCounts CountTenantScopedRecords(string tenantId, bool includeAuditRecords, bool includeRequestHistory)
        {
            if (String.IsNullOrWhiteSpace(tenantId)) throw new ArgumentException("Tenant ID is required.", nameof(tenantId));

            return new AdminNukeCounts
            {
                Tenants = Tenants.Count(item => String.Equals(item.Id, tenantId, StringComparison.OrdinalIgnoreCase)),
                Users = Users.Count(item => String.Equals(item.TenantId, tenantId, StringComparison.OrdinalIgnoreCase)),
                Credentials = Credentials.Count(item => String.Equals(item.TenantId, tenantId, StringComparison.OrdinalIgnoreCase)),
                AuthSessions = AuthSessions.Count(item => String.Equals(item.TenantId, tenantId, StringComparison.OrdinalIgnoreCase)),
                Roles = Roles.Count(item => String.Equals(item.TenantId, tenantId, StringComparison.OrdinalIgnoreCase)),
                Permissions = Permissions.Count(item => String.Equals(item.TenantId, tenantId, StringComparison.OrdinalIgnoreCase)),
                RoleAssignments = RoleAssignments.Count(item => String.Equals(item.TenantId, tenantId, StringComparison.OrdinalIgnoreCase)),
                Clients = Clients.Count(item => String.Equals(item.TenantId, tenantId, StringComparison.OrdinalIgnoreCase)),
                ModelDefinitions = ModelDefinitions.Count(item => String.Equals(item.TenantId, tenantId, StringComparison.OrdinalIgnoreCase)),
                ModelConfigurations = ModelConfigurations.Count(item => String.Equals(item.TenantId, tenantId, StringComparison.OrdinalIgnoreCase)),
                RoutingPolicies = RoutingPolicies.Count(item => String.Equals(item.TenantId, tenantId, StringComparison.OrdinalIgnoreCase))
            };
        }

        /// <summary>
        /// Remove tenant-scoped records for a nuke operation.
        /// </summary>
        /// <param name="tenantId">Tenant ID.</param>
        /// <param name="includeAuditRecords">Whether audit records are included.</param>
        /// <param name="includeRequestHistory">Whether request history is included.</param>
        /// <returns>Deleted object counts.</returns>
        public AdminNukeCounts RemoveTenantScopedRecords(string tenantId, bool includeAuditRecords, bool includeRequestHistory)
        {
            if (String.IsNullOrWhiteSpace(tenantId)) throw new ArgumentException("Tenant ID is required.", nameof(tenantId));

            AdminNukeCounts counts = new AdminNukeCounts
            {
                Tenants = Tenants.RemoveAll(item => String.Equals(item.Id, tenantId, StringComparison.OrdinalIgnoreCase)),
                Users = Users.RemoveAll(item => String.Equals(item.TenantId, tenantId, StringComparison.OrdinalIgnoreCase)),
                Credentials = Credentials.RemoveAll(item => String.Equals(item.TenantId, tenantId, StringComparison.OrdinalIgnoreCase)),
                AuthSessions = AuthSessions.RemoveAll(item => String.Equals(item.TenantId, tenantId, StringComparison.OrdinalIgnoreCase)),
                Roles = Roles.RemoveAll(item => String.Equals(item.TenantId, tenantId, StringComparison.OrdinalIgnoreCase)),
                Permissions = Permissions.RemoveAll(item => String.Equals(item.TenantId, tenantId, StringComparison.OrdinalIgnoreCase)),
                RoleAssignments = RoleAssignments.RemoveAll(item => String.Equals(item.TenantId, tenantId, StringComparison.OrdinalIgnoreCase)),
                Clients = Clients.RemoveAll(item => String.Equals(item.TenantId, tenantId, StringComparison.OrdinalIgnoreCase)),
                ModelDefinitions = ModelDefinitions.RemoveAll(item => String.Equals(item.TenantId, tenantId, StringComparison.OrdinalIgnoreCase)),
                ModelConfigurations = ModelConfigurations.RemoveAll(item => String.Equals(item.TenantId, tenantId, StringComparison.OrdinalIgnoreCase)),
                RoutingPolicies = RoutingPolicies.RemoveAll(item => String.Equals(item.TenantId, tenantId, StringComparison.OrdinalIgnoreCase))
            };

            LedgerTenantLinks.RemoveAll(item => String.Equals(item.XenoTenantId, tenantId, StringComparison.OrdinalIgnoreCase));
            LedgerAccountLinks.RemoveAll(item => String.Equals(item.XenoTenantId, tenantId, StringComparison.OrdinalIgnoreCase));

            return counts;
        }

        /// <summary>
        /// Count user-scoped records for a nuke operation.
        /// </summary>
        /// <param name="tenantId">Tenant ID.</param>
        /// <param name="userId">User ID.</param>
        /// <param name="includeAuditRecords">Whether audit records are included.</param>
        /// <param name="anonymizeRequestHistory">Whether request history references are anonymized.</param>
        /// <returns>Object counts.</returns>
        public AdminNukeResponse CountUserScopedRecords(string tenantId, string userId, bool includeAuditRecords, bool anonymizeRequestHistory)
        {
            if (String.IsNullOrWhiteSpace(tenantId)) throw new ArgumentException("Tenant ID is required.", nameof(tenantId));
            if (String.IsNullOrWhiteSpace(userId)) throw new ArgumentException("User ID is required.", nameof(userId));

            List<string> credentialIds = Credentials
                .Where(item => String.Equals(item.TenantId, tenantId, StringComparison.OrdinalIgnoreCase) && String.Equals(item.UserId, userId, StringComparison.OrdinalIgnoreCase))
                .Select(item => item.Id)
                .ToList();
            int sessionCount = AuthSessions
                .Count(item => String.Equals(item.TenantId, tenantId, StringComparison.OrdinalIgnoreCase) && (String.Equals(item.UserId, userId, StringComparison.OrdinalIgnoreCase) || (!String.IsNullOrWhiteSpace(item.CredentialId) && credentialIds.Contains(item.CredentialId))));

            AdminNukeResponse response = new AdminNukeResponse
            {
                Deleted = new AdminNukeCounts
                {
                    Users = Users.Count(item => String.Equals(item.Id, userId, StringComparison.OrdinalIgnoreCase) && String.Equals(item.TenantId, tenantId, StringComparison.OrdinalIgnoreCase)),
                    Credentials = credentialIds.Count,
                    AuthSessions = sessionCount,
                    RoleAssignments = RoleAssignments.Count(item => String.Equals(item.TenantId, tenantId, StringComparison.OrdinalIgnoreCase) && ((item.PrincipalType == PrincipalTypeEnum.User && String.Equals(item.PrincipalId, userId, StringComparison.OrdinalIgnoreCase)) || (item.PrincipalType == PrincipalTypeEnum.Credential && credentialIds.Contains(item.PrincipalId))))
                },
                Updated = new AdminNukeUpdatedCounts()
            };
            return response;
        }

        /// <summary>
        /// Remove user-scoped records for a nuke operation.
        /// </summary>
        /// <param name="tenantId">Tenant ID.</param>
        /// <param name="userId">User ID.</param>
        /// <param name="includeAuditRecords">Whether audit records are included.</param>
        /// <param name="anonymizeRequestHistory">Whether request history references are anonymized.</param>
        /// <returns>Deleted and updated object counts.</returns>
        public AdminNukeResponse RemoveUserScopedRecords(string tenantId, string userId, bool includeAuditRecords, bool anonymizeRequestHistory)
        {
            if (String.IsNullOrWhiteSpace(tenantId)) throw new ArgumentException("Tenant ID is required.", nameof(tenantId));
            if (String.IsNullOrWhiteSpace(userId)) throw new ArgumentException("User ID is required.", nameof(userId));

            List<string> credentialIds = Credentials
                .Where(item => String.Equals(item.TenantId, tenantId, StringComparison.OrdinalIgnoreCase) && String.Equals(item.UserId, userId, StringComparison.OrdinalIgnoreCase))
                .Select(item => item.Id)
                .ToList();

            AdminNukeResponse response = new AdminNukeResponse
            {
                Deleted = new AdminNukeCounts
                {
                    Users = Users.RemoveAll(item => String.Equals(item.Id, userId, StringComparison.OrdinalIgnoreCase) && String.Equals(item.TenantId, tenantId, StringComparison.OrdinalIgnoreCase)),
                    Credentials = Credentials.RemoveAll(item => String.Equals(item.TenantId, tenantId, StringComparison.OrdinalIgnoreCase) && String.Equals(item.UserId, userId, StringComparison.OrdinalIgnoreCase)),
                    AuthSessions = AuthSessions.RemoveAll(item => String.Equals(item.TenantId, tenantId, StringComparison.OrdinalIgnoreCase) && (String.Equals(item.UserId, userId, StringComparison.OrdinalIgnoreCase) || (!String.IsNullOrWhiteSpace(item.CredentialId) && credentialIds.Contains(item.CredentialId)))),
                    RoleAssignments = RoleAssignments.RemoveAll(item => String.Equals(item.TenantId, tenantId, StringComparison.OrdinalIgnoreCase) && ((item.PrincipalType == PrincipalTypeEnum.User && String.Equals(item.PrincipalId, userId, StringComparison.OrdinalIgnoreCase)) || (item.PrincipalType == PrincipalTypeEnum.Credential && credentialIds.Contains(item.PrincipalId))))
                }
            };
            LedgerAccountLinks.RemoveAll(item => String.Equals(item.XenoTenantId, tenantId, StringComparison.OrdinalIgnoreCase) && String.Equals(item.XenoUserId, userId, StringComparison.OrdinalIgnoreCase));

            return response;
        }

        /// <summary>
        /// Count remaining records that still reference a tenant identifier.
        /// </summary>
        /// <param name="tenantId">Tenant ID.</param>
        /// <returns>Remaining record count.</returns>
        public int CountTenantRemnants(string tenantId)
        {
            if (String.IsNullOrWhiteSpace(tenantId)) throw new ArgumentException("Tenant ID is required.", nameof(tenantId));

            int count = Tenants.Count(item => String.Equals(item.Id, tenantId, StringComparison.OrdinalIgnoreCase));
            count += Users.Count(item => String.Equals(item.TenantId, tenantId, StringComparison.OrdinalIgnoreCase));
            count += Credentials.Count(item => String.Equals(item.TenantId, tenantId, StringComparison.OrdinalIgnoreCase));
            count += AuthSessions.Count(item => String.Equals(item.TenantId, tenantId, StringComparison.OrdinalIgnoreCase));
            count += Roles.Count(item => String.Equals(item.TenantId, tenantId, StringComparison.OrdinalIgnoreCase));
            count += Permissions.Count(item => String.Equals(item.TenantId, tenantId, StringComparison.OrdinalIgnoreCase));
            count += RoleAssignments.Count(item => String.Equals(item.TenantId, tenantId, StringComparison.OrdinalIgnoreCase));
            count += Clients.Count(item => String.Equals(item.TenantId, tenantId, StringComparison.OrdinalIgnoreCase));
            count += ModelDefinitions.Count(item => String.Equals(item.TenantId, tenantId, StringComparison.OrdinalIgnoreCase));
            count += ModelConfigurations.Count(item => String.Equals(item.TenantId, tenantId, StringComparison.OrdinalIgnoreCase));
            count += RoutingPolicies.Count(item => String.Equals(item.TenantId, tenantId, StringComparison.OrdinalIgnoreCase));
            count += LedgerTenantLinks.Count(item => String.Equals(item.XenoTenantId, tenantId, StringComparison.OrdinalIgnoreCase));
            count += LedgerAccountLinks.Count(item => String.Equals(item.XenoTenantId, tenantId, StringComparison.OrdinalIgnoreCase));
            return count;
        }

        /// <summary>
        /// Count remaining records that still reference a user identifier.
        /// </summary>
        /// <param name="tenantId">Tenant ID.</param>
        /// <param name="userId">User ID.</param>
        /// <returns>Remaining record count.</returns>
        public int CountUserRemnants(string tenantId, string userId)
        {
            if (String.IsNullOrWhiteSpace(tenantId)) throw new ArgumentException("Tenant ID is required.", nameof(tenantId));
            if (String.IsNullOrWhiteSpace(userId)) throw new ArgumentException("User ID is required.", nameof(userId));

            int count = Users.Count(item => String.Equals(item.TenantId, tenantId, StringComparison.OrdinalIgnoreCase) && String.Equals(item.Id, userId, StringComparison.OrdinalIgnoreCase));
            count += Credentials.Count(item => String.Equals(item.TenantId, tenantId, StringComparison.OrdinalIgnoreCase) && String.Equals(item.UserId, userId, StringComparison.OrdinalIgnoreCase));
            count += AuthSessions.Count(item => String.Equals(item.TenantId, tenantId, StringComparison.OrdinalIgnoreCase) && String.Equals(item.UserId, userId, StringComparison.OrdinalIgnoreCase));
            count += RoleAssignments.Count(item => String.Equals(item.TenantId, tenantId, StringComparison.OrdinalIgnoreCase) && String.Equals(item.PrincipalId, userId, StringComparison.OrdinalIgnoreCase));
            count += LedgerAccountLinks.Count(item => String.Equals(item.XenoTenantId, tenantId, StringComparison.OrdinalIgnoreCase) && String.Equals(item.XenoUserId, userId, StringComparison.OrdinalIgnoreCase));
            return count;
        }

        /// <summary>
        /// Create a serializable snapshot of platform state.
        /// </summary>
        /// <returns>Platform state snapshot.</returns>
        public PlatformState ToState()
        {
            return new PlatformState
            {
                Tenants = Tenants,
                Users = Users,
                Credentials = Credentials,
                AuthSessions = AuthSessions,
                Roles = Roles,
                Permissions = Permissions,
                RoleAssignments = RoleAssignments,
                Clients = Clients,
                ModelDefinitions = ModelDefinitions,
                ModelConfigurations = ModelConfigurations,
                RoutingPolicies = RoutingPolicies,
                LedgerTenantLinks = LedgerTenantLinks,
                LedgerAccountLinks = LedgerAccountLinks
            };
        }

        /// <summary>
        /// Replace current platform state from a serialized snapshot.
        /// </summary>
        /// <param name="state">Platform state snapshot.</param>
        public void LoadState(PlatformState state)
        {
            if (state == null) throw new ArgumentNullException(nameof(state));

            Tenants.Clear();
            Tenants.AddRange(state.Tenants);

            Users.Clear();
            Users.AddRange(state.Users);

            Credentials.Clear();
            Credentials.AddRange(state.Credentials);

            AuthSessions.Clear();
            AuthSessions.AddRange(state.AuthSessions);

            Roles.Clear();
            Roles.AddRange(state.Roles);

            Permissions.Clear();
            Permissions.AddRange(state.Permissions);

            RoleAssignments.Clear();
            RoleAssignments.AddRange(state.RoleAssignments);

            Clients.Clear();
            Clients.AddRange(state.Clients);

            ModelDefinitions.Clear();
            ModelDefinitions.AddRange(state.ModelDefinitions);

            ModelConfigurations.Clear();
            ModelConfigurations.AddRange(state.ModelConfigurations);

            RoutingPolicies.Clear();
            RoutingPolicies.AddRange(state.RoutingPolicies);

            LedgerTenantLinks.Clear();
            LedgerTenantLinks.AddRange(state.LedgerTenantLinks);

            LedgerAccountLinks.Clear();
            LedgerAccountLinks.AddRange(state.LedgerAccountLinks);

            EnsureTenantDefaults();
        }

        /// <summary>
        /// Resolve the default tenant.
        /// </summary>
        /// <returns>Tenant.</returns>
        public Tenant GetDefaultTenant()
        {
            Tenant? tenant = Tenants.FirstOrDefault(item => item.Active);
            if (tenant == null) throw new InvalidOperationException("No active tenant exists.");
            return tenant;
        }

        private void EnsureDefaultRoutingPolicy(string tenantId)
        {
            RoutingPolicy? existing = RoutingPolicies.FirstOrDefault(item =>
                item.TenantId == tenantId &&
                item.IsDefault);
            if (existing != null)
            {
                existing.ModelName = "*";
                existing.ClientName = "*";
                if (existing.RequestTimeoutMs < 1000) existing.RequestTimeoutMs = 300000;
                if (!Enum.IsDefined(typeof(LoadBalancingModeEnum), existing.LoadBalancingMode)) existing.LoadBalancingMode = LoadBalancingModeEnum.LeastRecentlyUsed;
                existing.Active = true;
                return;
            }

            RoutingPolicies.Add(new RoutingPolicy
            {
                TenantId = tenantId,
                Name = "Default Routing Policy",
                ModelName = "*",
                ClientName = "*",
                RequestTimeoutMs = 300000,
                LoadBalancingMode = LoadBalancingModeEnum.LeastRecentlyUsed,
                Adaptive = new AdaptiveLoadBalancingSettings(),
                IsDefault = true,
                Active = true
            });
        }

        private void EnsureDefaultTenantSecurity(string tenantId)
        {
            User? adminUser = Users.FirstOrDefault(item => item.TenantId == tenantId && String.Equals(item.Email, "admin@xeno", StringComparison.OrdinalIgnoreCase));
            if (adminUser != null && !Credentials.Any(item => item.TenantId == tenantId && String.Equals(item.AccessKey, "default", StringComparison.Ordinal)))
            {
                Credentials.Add(new Credential
                {
                    TenantId = tenantId,
                    UserId = adminUser.Id,
                    AccessKey = "default",
                    SecretKey = "default",
                    Active = true
                });
            }

            Permission readPermission = EnsurePermission(tenantId, "*", OperationTypeEnum.Read);
            Permission writePermission = EnsurePermission(tenantId, "*", OperationTypeEnum.Write);
            Permission deletePermission = EnsurePermission(tenantId, "*", OperationTypeEnum.Delete);
            Permission executePermission = EnsurePermission(tenantId, "*", OperationTypeEnum.Execute);
            Permission authenticationReadPermission = EnsurePermission(tenantId, "Authentication", OperationTypeEnum.Read);
            Permission tenantsReadPermission = EnsurePermission(tenantId, "Tenants", OperationTypeEnum.Read);
            Permission usersReadPermission = EnsurePermission(tenantId, "Users", OperationTypeEnum.Read);
            Permission credentialsReadPermission = EnsurePermission(tenantId, "Credentials", OperationTypeEnum.Read);
            Permission credentialsWritePermission = EnsurePermission(tenantId, "Credentials", OperationTypeEnum.Write);
            Permission rolesReadPermission = EnsurePermission(tenantId, "Roles", OperationTypeEnum.Read);
            Permission permissionsReadPermission = EnsurePermission(tenantId, "Permissions", OperationTypeEnum.Read);
            Permission clientsReadPermission = EnsurePermission(tenantId, "Clients", OperationTypeEnum.Read);
            Permission clientTelemetryReadPermission = EnsurePermission(tenantId, "ClientTelemetry", OperationTypeEnum.Read);
            Permission inferenceReadPermission = EnsurePermission(tenantId, "Inference", OperationTypeEnum.Read);
            Permission inferenceExecutePermission = EnsurePermission(tenantId, "Inference", OperationTypeEnum.Execute);
            Permission ledgerReadPermission = EnsurePermission(tenantId, "Ledger", OperationTypeEnum.Read);
            Permission ledgerAdminPermission = EnsurePermission(tenantId, "Ledger", OperationTypeEnum.Admin);
            Permission adminPermission = EnsurePermission(tenantId, "Admin", OperationTypeEnum.Admin);
            Permission chatReadPermission = EnsurePermission(tenantId, "Chat", OperationTypeEnum.Read);
            Permission chatWritePermission = EnsurePermission(tenantId, "Chat", OperationTypeEnum.Write);
            Permission chatDeletePermission = EnsurePermission(tenantId, "Chat", OperationTypeEnum.Delete);
            Permission chatExecutePermission = EnsurePermission(tenantId, "Chat", OperationTypeEnum.Execute);
            Role tenantAdmin = EnsureRole(tenantId, "TenantAdmin", new List<string> { readPermission.Id, writePermission.Id, deletePermission.Id, executePermission.Id, inferenceReadPermission.Id, inferenceExecutePermission.Id, ledgerReadPermission.Id, ledgerAdminPermission.Id, adminPermission.Id });
            EnsureRole(tenantId, "Viewer", new List<string> { readPermission.Id });
            Role tenantMember = EnsureRole(tenantId, "TenantMember", new List<string>
            {
                authenticationReadPermission.Id,
                tenantsReadPermission.Id,
                usersReadPermission.Id,
                credentialsReadPermission.Id,
                credentialsWritePermission.Id,
                rolesReadPermission.Id,
                permissionsReadPermission.Id,
                clientsReadPermission.Id,
                clientTelemetryReadPermission.Id,
                inferenceReadPermission.Id,
                ledgerReadPermission.Id,
                chatReadPermission.Id,
                chatWritePermission.Id,
                chatDeletePermission.Id,
                chatExecutePermission.Id
            });
            EnsureTenantMemberBackfill(tenantId, tenantMember);

            if (adminUser != null)
            {
                bool hasAssignment = RoleAssignments.Any(item =>
                    item.TenantId == tenantId &&
                    item.RoleId == tenantAdmin.Id &&
                    item.PrincipalType == PrincipalTypeEnum.User &&
                    item.PrincipalId == adminUser.Id);
                if (!hasAssignment)
                {
                    RoleAssignments.Add(new RoleAssignment
                    {
                        TenantId = tenantId,
                        RoleId = tenantAdmin.Id,
                        PrincipalType = PrincipalTypeEnum.User,
                        PrincipalId = adminUser.Id
                    });
                }
            }

            Credential? defaultCredential = Credentials.FirstOrDefault(item => item.TenantId == tenantId && String.Equals(item.AccessKey, "default", StringComparison.Ordinal));
            if (defaultCredential != null)
            {
                bool hasAssignment = RoleAssignments.Any(item =>
                    item.TenantId == tenantId &&
                    item.RoleId == tenantAdmin.Id &&
                    item.PrincipalType == PrincipalTypeEnum.Credential &&
                    item.PrincipalId == defaultCredential.Id);
                if (!hasAssignment)
                {
                    RoleAssignments.Add(new RoleAssignment
                    {
                        TenantId = tenantId,
                        RoleId = tenantAdmin.Id,
                        PrincipalType = PrincipalTypeEnum.Credential,
                        PrincipalId = defaultCredential.Id
                    });
                }
            }
        }

        private void EnsureTenantMemberBackfill(string tenantId, Role tenantMember)
        {
            List<User> regularUsers = Users
                .Where(item => item.TenantId == tenantId)
                .Where(item => item.Active)
                .Where(item => !item.IsAdmin && !item.IsTenantAdmin)
                .ToList();
            foreach (User user in regularUsers)
            {
                EnsureTenantMemberAssignment(tenantId, tenantMember.Id, PrincipalTypeEnum.User, user.Id);
            }

            List<string> regularUserIds = regularUsers.Select(item => item.Id).ToList();
            foreach (Credential credential in Credentials.Where(item => item.TenantId == tenantId && item.Active && regularUserIds.Contains(item.UserId)).ToList())
            {
                EnsureTenantMemberAssignment(tenantId, tenantMember.Id, PrincipalTypeEnum.Credential, credential.Id);
            }
        }

        private void EnsureTenantMemberAssignment(string tenantId, string roleId, PrincipalTypeEnum principalType, string principalId)
        {
            bool hasActiveAssignment = RoleAssignments.Any(item =>
                item.TenantId == tenantId &&
                item.PrincipalType == principalType &&
                item.PrincipalId == principalId &&
                item.Active);
            if (hasActiveAssignment) return;

            RoleAssignments.Add(new RoleAssignment
            {
                TenantId = tenantId,
                RoleId = roleId,
                PrincipalType = principalType,
                PrincipalId = principalId,
                Active = true
            });
        }

        private Permission EnsurePermission(string tenantId, string resourceType, OperationTypeEnum operationType)
        {
            Permission? permission = Permissions.FirstOrDefault(item =>
                item.TenantId == tenantId &&
                String.Equals(item.ResourceType, resourceType, StringComparison.OrdinalIgnoreCase) &&
                item.OperationType == operationType);
            if (permission != null)
            {
                permission.Permit = true;
                permission.Active = true;
                permission.BuiltIn = true;
                return permission;
            }

            permission = new Permission
            {
                TenantId = tenantId,
                ResourceType = resourceType,
                OperationType = operationType,
                Permit = true,
                BuiltIn = true,
                Active = true
            };
            Permissions.Add(permission);
            return permission;
        }

        private Role EnsureRole(string tenantId, string name, List<string> permissionIds)
        {
            Role? role = Roles.FirstOrDefault(item =>
                item.TenantId == tenantId &&
                String.Equals(item.Name, name, StringComparison.OrdinalIgnoreCase));
            if (role != null)
            {
                role.BuiltIn = true;
                role.Active = true;
                foreach (string permissionId in permissionIds)
                {
                    if (!role.PermissionIds.Contains(permissionId, StringComparer.OrdinalIgnoreCase)) role.PermissionIds.Add(permissionId);
                }

                return role;
            }

            role = new Role
            {
                TenantId = tenantId,
                Name = name,
                BuiltIn = true,
                PermissionIds = permissionIds,
                Active = true
            };
            Roles.Add(role);
            return role;
        }
    }
}
