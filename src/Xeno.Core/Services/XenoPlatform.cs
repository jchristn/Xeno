namespace Xeno.Core.Services
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Threading;
    using System.Threading.Tasks;
    using Xeno.Core.Models;

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
        /// Endpoints.
        /// </summary>
        public List<ModelRunnerEndpoint> Endpoints { get; } = new List<ModelRunnerEndpoint>();

        /// <summary>
        /// Model definitions.
        /// </summary>
        public List<ModelDefinition> ModelDefinitions { get; } = new List<ModelDefinition>();

        /// <summary>
        /// Model configurations.
        /// </summary>
        public List<ModelConfiguration> ModelConfigurations { get; } = new List<ModelConfiguration>();

        /// <summary>
        /// Load-balancing policies.
        /// </summary>
        public List<LoadBalancingPolicy> LoadBalancingPolicies { get; } = new List<LoadBalancingPolicy>();

        /// <summary>
        /// Model access policies.
        /// </summary>
        public List<ModelAccessPolicy> ModelAccessPolicies { get; } = new List<ModelAccessPolicy>();

        /// <summary>
        /// Virtual model runners.
        /// </summary>
        public List<VirtualModelRunner> VirtualModelRunners { get; } = new List<VirtualModelRunner>();

        /// <summary>
        /// Virtual model runner reservations.
        /// </summary>
        public List<VirtualModelRunnerReservation> VirtualModelRunnerReservations { get; } = new List<VirtualModelRunnerReservation>();

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
            authentication.SetUserPassword(user, "xeno");
            Users.Add(user);

            Credential credential = new Credential
            {
                TenantId = tenant.Id,
                UserId = user.Id,
                AccessKey = "access_development",
                SecretKey = "secret_development",
                Active = true
            };
            Credentials.Add(credential);

            Permission readPermission = new Permission
            {
                TenantId = tenant.Id,
                ResourceType = "*",
                OperationType = Xeno.Core.Enums.OperationTypeEnum.Read,
                Permit = true
            };
            Permissions.Add(readPermission);

            Permission writePermission = new Permission
            {
                TenantId = tenant.Id,
                ResourceType = "*",
                OperationType = Xeno.Core.Enums.OperationTypeEnum.Write,
                Permit = true
            };
            Permissions.Add(writePermission);

            Permission deletePermission = new Permission
            {
                TenantId = tenant.Id,
                ResourceType = "*",
                OperationType = Xeno.Core.Enums.OperationTypeEnum.Delete,
                Permit = true
            };
            Permissions.Add(deletePermission);

            Permission adminPermission = new Permission
            {
                TenantId = tenant.Id,
                ResourceType = "Admin",
                OperationType = Xeno.Core.Enums.OperationTypeEnum.Admin,
                Permit = true
            };
            Permissions.Add(adminPermission);

            Role tenantAdmin = new Role
            {
                TenantId = tenant.Id,
                Name = "TenantAdmin",
                BuiltIn = true,
                PermissionIds = new List<string> { readPermission.Id, writePermission.Id, deletePermission.Id, adminPermission.Id }
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
                Connected = false
            };
            Clients.Add(client);

            ModelRunnerEndpoint endpoint = new ModelRunnerEndpoint
            {
                TenantId = tenant.Id,
                ClientId = client.Id,
                Name = "local-ollama",
                Active = true
            };
            Endpoints.Add(endpoint);

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

            LoadBalancingPolicy loadBalancingPolicy = new LoadBalancingPolicy
            {
                TenantId = tenant.Id,
                Name = "Default least in-flight",
                RankingMetric = "inFlightRequests",
                PreferLower = true
            };
            LoadBalancingPolicies.Add(loadBalancingPolicy);

            ModelAccessPolicy modelAccessPolicy = new ModelAccessPolicy
            {
                TenantId = tenant.Id,
                Name = "Default permit",
                Mode = Xeno.Core.Enums.ModelAccessModeEnum.Monitor
            };
            ModelAccessPolicies.Add(modelAccessPolicy);

            VirtualModelRunner vmr = new VirtualModelRunner
            {
                TenantId = tenant.Id,
                Name = "default",
                ModelRunnerEndpointIds = new List<string> { endpoint.Id },
                ModelDefinitionIds = new List<string> { chatModel.Id, embeddingModel.Id },
                LoadBalancingPolicyId = loadBalancingPolicy.Id,
                ModelAccessPolicyId = modelAccessPolicy.Id
            };
            VirtualModelRunners.Add(vmr);

            VirtualModelRunnerReservations.Add(new VirtualModelRunnerReservation
            {
                TenantId = tenant.Id,
                VirtualModelRunnerId = vmr.Id,
                Name = "example-reservation",
                Active = false
            });

            return Task.CompletedTask;
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
                Endpoints = Endpoints,
                ModelDefinitions = ModelDefinitions,
                ModelConfigurations = ModelConfigurations,
                LoadBalancingPolicies = LoadBalancingPolicies,
                ModelAccessPolicies = ModelAccessPolicies,
                VirtualModelRunners = VirtualModelRunners,
                VirtualModelRunnerReservations = VirtualModelRunnerReservations,
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

            Endpoints.Clear();
            Endpoints.AddRange(state.Endpoints);

            ModelDefinitions.Clear();
            ModelDefinitions.AddRange(state.ModelDefinitions);

            ModelConfigurations.Clear();
            ModelConfigurations.AddRange(state.ModelConfigurations);

            LoadBalancingPolicies.Clear();
            LoadBalancingPolicies.AddRange(state.LoadBalancingPolicies);

            ModelAccessPolicies.Clear();
            ModelAccessPolicies.AddRange(state.ModelAccessPolicies);

            VirtualModelRunners.Clear();
            VirtualModelRunners.AddRange(state.VirtualModelRunners);

            VirtualModelRunnerReservations.Clear();
            VirtualModelRunnerReservations.AddRange(state.VirtualModelRunnerReservations);

            RequestHistory.Clear();
            RequestHistory.AddRange(state.RequestHistory);
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
    }
}
