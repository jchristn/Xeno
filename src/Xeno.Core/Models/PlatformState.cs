namespace Xeno.Core.Models
{
    using System.Collections.Generic;

    /// <summary>
    /// Serializable platform state persisted by database drivers.
    /// </summary>
    public class PlatformState
    {
        /// <summary>
        /// Tenants.
        /// </summary>
        public List<Tenant> Tenants { get; set; } = new List<Tenant>();

        /// <summary>
        /// Users.
        /// </summary>
        public List<User> Users { get; set; } = new List<User>();

        /// <summary>
        /// Credentials.
        /// </summary>
        public List<Credential> Credentials { get; set; } = new List<Credential>();

        /// <summary>
        /// Auth sessions.
        /// </summary>
        public List<AuthSession> AuthSessions { get; set; } = new List<AuthSession>();

        /// <summary>
        /// Roles.
        /// </summary>
        public List<Role> Roles { get; set; } = new List<Role>();

        /// <summary>
        /// Permissions.
        /// </summary>
        public List<Permission> Permissions { get; set; } = new List<Permission>();

        /// <summary>
        /// Role assignments.
        /// </summary>
        public List<RoleAssignment> RoleAssignments { get; set; } = new List<RoleAssignment>();

        /// <summary>
        /// Audit records.
        /// </summary>
        public List<AuditRecord> AuditRecords { get; set; } = new List<AuditRecord>();

        /// <summary>
        /// Clients.
        /// </summary>
        public List<XenoClientRecord> Clients { get; set; } = new List<XenoClientRecord>();

        /// <summary>
        /// Model definitions.
        /// </summary>
        public List<ModelDefinition> ModelDefinitions { get; set; } = new List<ModelDefinition>();

        /// <summary>
        /// Model configurations.
        /// </summary>
        public List<ModelConfiguration> ModelConfigurations { get; set; } = new List<ModelConfiguration>();

        /// <summary>
        /// Routing policies.
        /// </summary>
        public List<RoutingPolicy> RoutingPolicies { get; set; } = new List<RoutingPolicy>();

        /// <summary>
        /// Request history entries.
        /// </summary>
        public List<RequestHistoryEntry> RequestHistory { get; set; } = new List<RequestHistoryEntry>();

        /// <summary>
        /// Ledger tenant links.
        /// </summary>
        public List<LedgerTenantLink> LedgerTenantLinks { get; set; } = new List<LedgerTenantLink>();

        /// <summary>
        /// Ledger account links.
        /// </summary>
        public List<LedgerAccountLink> LedgerAccountLinks { get; set; } = new List<LedgerAccountLink>();
    }
}
