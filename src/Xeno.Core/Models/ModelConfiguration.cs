namespace Xeno.Core.Models
{
    using System;
    using System.Collections.Generic;
    using Xeno.Core.Enums;
    using Xeno.Core.Helpers;

    /// <summary>
    /// Model configuration.
    /// </summary>
    public class ModelConfiguration
    {
        /// <summary>
        /// Configuration ID.
        /// </summary>
        public string Id { get; set; } = IdGenerator.NewId("mc_");

        /// <summary>
        /// Tenant ID.
        /// </summary>
        public string TenantId { get; set; } = Constants.DefaultTenantId;

        /// <summary>
        /// Configuration name.
        /// </summary>
        public string Name { get; set; } = "Default";

        /// <summary>
        /// Optional model selector.
        /// </summary>
        public string? Model { get; set; } = null;

        /// <summary>
        /// Pinned completions properties.
        /// </summary>
        public Dictionary<string, object> PinnedCompletionsProperties { get; set; } = new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase);
    }
}
