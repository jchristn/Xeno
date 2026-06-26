namespace Xeno.Core.Models
{
    using System;
    using Xeno.Core;
    using Xeno.Core.Enums;
    using Xeno.Core.Helpers;
    using Xeno.Core.Settings;

    /// <summary>
    /// Tenant-scoped routing policy for model requests.
    /// </summary>
    public class RoutingPolicy
    {
        /// <summary>
        /// Routing policy ID.
        /// </summary>
        public string Id { get; set; } = IdGenerator.NewId("rpol_");

        /// <summary>
        /// Tenant ID.
        /// </summary>
        public string TenantId { get; set; } = Constants.DefaultTenantId;

        /// <summary>
        /// Policy name.
        /// </summary>
        public string Name { get; set; } = "Default Routing Policy";

        /// <summary>
        /// Model name match expression. Use "*" to match all models.
        /// </summary>
        public string ModelName { get; set; } = "*";

        /// <summary>
        /// Client name match expression. Use "*" to match all clients.
        /// </summary>
        public string ClientName { get; set; } = "*";

        /// <summary>
        /// Request timeout in milliseconds.
        /// </summary>
        public int RequestTimeoutMs { get; set; } = 300000;

        /// <summary>
        /// Load-balancing mode used when this policy is effective.
        /// </summary>
        public LoadBalancingModeEnum LoadBalancingMode { get; set; } = LoadBalancingModeEnum.LeastRecentlyUsed;

        /// <summary>
        /// Adaptive load-balancing settings used when LoadBalancingMode is Adaptive.
        /// </summary>
        public AdaptiveLoadBalancingSettings Adaptive { get; set; } = new AdaptiveLoadBalancingSettings();

        /// <summary>
        /// Whether this is the tenant fallback policy.
        /// </summary>
        public bool IsDefault { get; set; } = false;

        /// <summary>
        /// Whether the policy is active.
        /// </summary>
        public bool Active { get; set; } = true;

        /// <summary>
        /// Created timestamp.
        /// </summary>
        public DateTime CreatedUtc { get; set; } = DateTime.UtcNow;

        /// <summary>
        /// Last update timestamp.
        /// </summary>
        public DateTime LastUpdateUtc { get; set; } = DateTime.UtcNow;
    }
}
