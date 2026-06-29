namespace Xeno.Sdk
{
    using System;

    /// <summary>
    /// Xeno effective permission DTO.
    /// </summary>
    public class EffectivePermission
    {
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
        /// Gets or sets the source role.
        /// </summary>
        public string SourceRole { get; set; } = String.Empty;
    }
}
