namespace Xeno.Core.Models
{
    using System;
    using System.Collections.Generic;
    using Xeno.Core.Enums;
    using Xeno.Core.Helpers;

    /// <summary>
    /// Load-balancing policy.
    /// </summary>
    public class LoadBalancingPolicy
    {
        /// <summary>
        /// Policy ID.
        /// </summary>
        public string Id { get; set; } = IdGenerator.NewId("lbp_");

        /// <summary>
        /// Tenant ID.
        /// </summary>
        public string TenantId { get; set; } = Constants.DefaultTenantId;

        /// <summary>
        /// Policy name.
        /// </summary>
        public string Name { get; set; } = "Least In-Flight";

        /// <summary>
        /// Whether policy is active.
        /// </summary>
        public bool Active { get; set; } = true;

        /// <summary>
        /// Ranking metric. Supported values include weight, inFlightRequests, and firstAvailable.
        /// </summary>
        public string RankingMetric { get; set; } = "inFlightRequests";

        /// <summary>
        /// Prefer lower metric values.
        /// </summary>
        public bool PreferLower { get; set; } = true;

        /// <summary>
        /// Whether the policy should fail closed when no endpoint survives filtering.
        /// </summary>
        public bool FailClosed { get; set; } = true;
    }
}
