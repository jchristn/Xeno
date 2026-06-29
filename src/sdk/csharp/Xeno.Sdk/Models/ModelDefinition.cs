namespace Xeno.Sdk
{
    using System;

    /// <summary>
    /// Xeno model definition DTO.
    /// </summary>
    public class ModelDefinition
    {
        /// <summary>
        /// Gets or sets the id.
        /// </summary>
        public string Id { get; set; } = String.Empty;

        /// <summary>
        /// Gets or sets the tenant id.
        /// </summary>
        public string TenantId { get; set; } = String.Empty;

        /// <summary>
        /// Gets or sets the name.
        /// </summary>
        public string Name { get; set; } = String.Empty;
    }
}
