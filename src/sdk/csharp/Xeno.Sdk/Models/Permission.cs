namespace Xeno.Sdk
{
    using System;

    /// <summary>
    /// Xeno permission DTO.
    /// </summary>
    public class Permission
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
        /// Gets or sets the resource type.
        /// </summary>
        public string ResourceType { get; set; } = String.Empty;

        /// <summary>
        /// Gets or sets the operation type.
        /// </summary>
        public string OperationType { get; set; } = String.Empty;

        /// <summary>
        /// Gets or sets the permit.
        /// </summary>
        public bool Permit { get; set; }

        /// <summary>
        /// Gets or sets the built in.
        /// </summary>
        public bool BuiltIn { get; set; }

        /// <summary>
        /// Gets or sets the active.
        /// </summary>
        public bool Active { get; set; }
    }
}
