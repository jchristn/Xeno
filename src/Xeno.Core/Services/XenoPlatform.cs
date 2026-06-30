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
        /// Audit records.
        /// </summary>
        public List<AuditRecord> AuditRecords { get; } = new List<AuditRecord>();

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
        /// Request history.
        /// </summary>
        public List<RequestHistoryEntry> RequestHistory { get; } = new List<RequestHistoryEntry>();

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

            int removed = Users.RemoveAll(item => item.TenantId == tenantId);
            removed += Credentials.RemoveAll(item => item.TenantId == tenantId);
            removed += AuthSessions.RemoveAll(item => item.TenantId == tenantId);
            removed += Roles.RemoveAll(item => item.TenantId == tenantId);
            removed += Permissions.RemoveAll(item => item.TenantId == tenantId);
            removed += RoleAssignments.RemoveAll(item => item.TenantId == tenantId);
            removed += AuditRecords.RemoveAll(item => item.TenantId == tenantId);
            removed += Clients.RemoveAll(item => item.TenantId == tenantId);
            removed += ModelDefinitions.RemoveAll(item => item.TenantId == tenantId);
            removed += ModelConfigurations.RemoveAll(item => item.TenantId == tenantId);
            removed += RoutingPolicies.RemoveAll(item => item.TenantId == tenantId);
            removed += RequestHistory.RemoveAll(item => item.TenantId == tenantId);
            return removed;
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
                AuditRecords = AuditRecords,
                Clients = Clients,
                ModelDefinitions = ModelDefinitions,
                ModelConfigurations = ModelConfigurations,
                RoutingPolicies = RoutingPolicies,
                RequestHistory = RequestHistory
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

            AuditRecords.Clear();
            AuditRecords.AddRange(state.AuditRecords);

            Clients.Clear();
            Clients.AddRange(state.Clients);

            ModelDefinitions.Clear();
            ModelDefinitions.AddRange(state.ModelDefinitions);

            ModelConfigurations.Clear();
            ModelConfigurations.AddRange(state.ModelConfigurations);

            RoutingPolicies.Clear();
            RoutingPolicies.AddRange(state.RoutingPolicies);

            RequestHistory.Clear();
            RequestHistory.AddRange(state.RequestHistory);

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
            Permission inferenceReadPermission = EnsurePermission(tenantId, "Inference", OperationTypeEnum.Read);
            Permission inferenceExecutePermission = EnsurePermission(tenantId, "Inference", OperationTypeEnum.Execute);
            Permission adminPermission = EnsurePermission(tenantId, "Admin", OperationTypeEnum.Admin);
            Role tenantAdmin = EnsureRole(tenantId, "TenantAdmin", new List<string> { readPermission.Id, writePermission.Id, deletePermission.Id, executePermission.Id, inferenceReadPermission.Id, inferenceExecutePermission.Id, adminPermission.Id });
            EnsureRole(tenantId, "Viewer", new List<string> { readPermission.Id });
            EnsureRole(tenantId, "TenantMember", new List<string> { readPermission.Id });

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
