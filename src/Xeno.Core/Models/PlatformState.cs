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
        /// Clients.
        /// </summary>
        public List<XenoClientRecord> Clients { get; set; } = new List<XenoClientRecord>();

        /// <summary>
        /// Model runner endpoints.
        /// </summary>
        public List<ModelRunnerEndpoint> Endpoints { get; set; } = new List<ModelRunnerEndpoint>();

        /// <summary>
        /// Model definitions.
        /// </summary>
        public List<ModelDefinition> ModelDefinitions { get; set; } = new List<ModelDefinition>();

        /// <summary>
        /// Model configurations.
        /// </summary>
        public List<ModelConfiguration> ModelConfigurations { get; set; } = new List<ModelConfiguration>();

        /// <summary>
        /// Load-balancing policies.
        /// </summary>
        public List<LoadBalancingPolicy> LoadBalancingPolicies { get; set; } = new List<LoadBalancingPolicy>();

        /// <summary>
        /// Model access policies.
        /// </summary>
        public List<ModelAccessPolicy> ModelAccessPolicies { get; set; } = new List<ModelAccessPolicy>();

        /// <summary>
        /// Virtual model runners.
        /// </summary>
        public List<VirtualModelRunner> VirtualModelRunners { get; set; } = new List<VirtualModelRunner>();

        /// <summary>
        /// Virtual model runner reservations.
        /// </summary>
        public List<VirtualModelRunnerReservation> VirtualModelRunnerReservations { get; set; } = new List<VirtualModelRunnerReservation>();

        /// <summary>
        /// Request history entries.
        /// </summary>
        public List<RequestHistoryEntry> RequestHistory { get; set; } = new List<RequestHistoryEntry>();
    }
}
