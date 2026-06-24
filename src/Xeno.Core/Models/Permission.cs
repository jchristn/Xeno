namespace Xeno.Core.Models
{
    using System;
    using Xeno.Core.Enums;
    using Xeno.Core.Helpers;

    /// <summary>
    /// Permission record.
    /// </summary>
    public class Permission
    {
        /// <summary>
        /// Permission ID.
        /// </summary>
        public string Id { get; set; } = IdGenerator.NewId("perm_");

        /// <summary>
        /// Tenant ID.
        /// </summary>
        public string TenantId { get; set; } = Constants.DefaultTenantId;

        /// <summary>
        /// Resource type.
        /// </summary>
        public string ResourceType { get; set; } = "*";

        /// <summary>
        /// Operation type.
        /// </summary>
        public OperationTypeEnum OperationType { get; set; } = OperationTypeEnum.Read;

        /// <summary>
        /// Whether this permission grants access.
        /// </summary>
        public bool Permit { get; set; } = true;

        /// <summary>
        /// Created timestamp.
        /// </summary>
        public DateTime CreatedUtc { get; set; } = DateTime.UtcNow;

        /// <summary>
        /// Last update timestamp.
        /// </summary>
        public DateTime LastUpdateUtc { get; set; } = DateTime.UtcNow;

        /// <summary>
        /// Whether the permission is active.
        /// </summary>
        public bool Active { get; set; } = true;
    }
}
