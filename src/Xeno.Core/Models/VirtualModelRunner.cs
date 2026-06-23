namespace Xeno.Core.Models
{
    using System;
    using System.Collections.Generic;
    using Xeno.Core.Enums;
    using Xeno.Core.Helpers;

    /// <summary>
    /// Virtual model runner.
    /// </summary>
    public class VirtualModelRunner
    {
        /// <summary>
        /// VMR ID.
        /// </summary>
        public string Id { get; set; } = IdGenerator.NewId("vmr_");

        /// <summary>
        /// Tenant ID.
        /// </summary>
        public string TenantId { get; set; } = Constants.DefaultTenantId;

        /// <summary>
        /// VMR name.
        /// </summary>
        public string Name { get; set; } = "default";

        /// <summary>
        /// Base path.
        /// </summary>
        public string BasePath { get; set; } = "/api/v1/openai/";

        /// <summary>
        /// Endpoint IDs.
        /// </summary>
        public List<string> ModelRunnerEndpointIds { get; set; } = new List<string>();

        /// <summary>
        /// Model definition IDs.
        /// </summary>
        public List<string> ModelDefinitionIds { get; set; } = new List<string>();

        /// <summary>
        /// Load-balancing mode.
        /// </summary>
        public LoadBalancingModeEnum LoadBalancingMode { get; set; } = LoadBalancingModeEnum.RoundRobin;

        /// <summary>
        /// Whether completions are allowed.
        /// </summary>
        public bool AllowCompletions { get; set; } = true;

        /// <summary>
        /// Whether embeddings are allowed.
        /// </summary>
        public bool AllowEmbeddings { get; set; } = true;

        /// <summary>
        /// Whether strict mode is enabled.
        /// </summary>
        public bool StrictMode { get; set; } = false;

        /// <summary>
        /// Whether the VMR is active.
        /// </summary>
        public bool Active { get; set; } = true;
    }
}
