namespace Xeno.Core.Models
{
    using System;
    using System.Collections.Generic;
    using Xeno.Core.Enums;
    using Xeno.Core.Helpers;

    /// <summary>
    /// Model access policy.
    /// </summary>
    public class ModelAccessPolicy
    {
        /// <summary>
        /// Policy ID.
        /// </summary>
        public string Id { get; set; } = IdGenerator.NewId("map_");

        /// <summary>
        /// Tenant ID.
        /// </summary>
        public string TenantId { get; set; } = Constants.DefaultTenantId;

        /// <summary>
        /// Policy name.
        /// </summary>
        public string Name { get; set; } = "Default Permit";

        /// <summary>
        /// Default decision.
        /// </summary>
        public ModelAccessDecisionEnum DefaultDecision { get; set; } = ModelAccessDecisionEnum.Permit;
    }
}
