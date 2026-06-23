namespace Xeno.Core.Models
{
    using System;
    using System.Collections.Generic;
    using Xeno.Core.Enums;
    using Xeno.Core.Helpers;

    /// <summary>
    /// Model definition.
    /// </summary>
    public class ModelDefinition
    {
        /// <summary>
        /// Model definition ID.
        /// </summary>
        public string Id { get; set; } = IdGenerator.NewId("md_");

        /// <summary>
        /// Tenant ID.
        /// </summary>
        public string TenantId { get; set; } = Constants.DefaultTenantId;

        /// <summary>
        /// Model name.
        /// </summary>
        public string Name { get; set; } = "llama3.2:latest";

        /// <summary>
        /// Whether embeddings are supported.
        /// </summary>
        public bool SupportsEmbeddings { get; set; } = false;

        /// <summary>
        /// Whether completions are supported.
        /// </summary>
        public bool SupportsCompletions { get; set; } = true;

        /// <summary>
        /// Labels.
        /// </summary>
        public List<string> Labels { get; set; } = new List<string>();
    }
}
