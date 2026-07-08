namespace Xeno.Core.Models
{
    using System.Text.Json.Serialization;

    /// <summary>
    /// Object counts affected by an administrative nuke operation.
    /// </summary>
    public class AdminNukeCounts
    {
        /// <summary>
        /// Tenant records.
        /// </summary>
        [JsonPropertyName("tenants")]
        public int Tenants { get; set; } = 0;

        /// <summary>
        /// User records.
        /// </summary>
        [JsonPropertyName("users")]
        public int Users { get; set; } = 0;

        /// <summary>
        /// Credential records.
        /// </summary>
        [JsonPropertyName("credentials")]
        public int Credentials { get; set; } = 0;

        /// <summary>
        /// Authentication session records.
        /// </summary>
        [JsonPropertyName("authSessions")]
        public int AuthSessions { get; set; } = 0;

        /// <summary>
        /// Role records.
        /// </summary>
        [JsonPropertyName("roles")]
        public int Roles { get; set; } = 0;

        /// <summary>
        /// Permission records.
        /// </summary>
        [JsonPropertyName("permissions")]
        public int Permissions { get; set; } = 0;

        /// <summary>
        /// Role assignment records.
        /// </summary>
        [JsonPropertyName("roleAssignments")]
        public int RoleAssignments { get; set; } = 0;

        /// <summary>
        /// Audit records.
        /// </summary>
        [JsonPropertyName("auditRecords")]
        public int AuditRecords { get; set; } = 0;

        /// <summary>
        /// Client records.
        /// </summary>
        [JsonPropertyName("clients")]
        public int Clients { get; set; } = 0;

        /// <summary>
        /// Model definition records.
        /// </summary>
        [JsonPropertyName("modelDefinitions")]
        public int ModelDefinitions { get; set; } = 0;

        /// <summary>
        /// Model configuration records.
        /// </summary>
        [JsonPropertyName("modelConfigurations")]
        public int ModelConfigurations { get; set; } = 0;

        /// <summary>
        /// Routing policy records.
        /// </summary>
        [JsonPropertyName("routingPolicies")]
        public int RoutingPolicies { get; set; } = 0;

        /// <summary>
        /// Request history records.
        /// </summary>
        [JsonPropertyName("requestHistory")]
        public int RequestHistory { get; set; } = 0;

        /// <summary>
        /// Provider-specific telemetry records.
        /// </summary>
        [JsonPropertyName("telemetryRecords")]
        public int TelemetryRecords { get; set; } = 0;
    }
}
