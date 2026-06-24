namespace Xeno.Core.Models
{
    using Xeno.Core.Enums;

    /// <summary>
    /// Effective permission returned for a principal.
    /// </summary>
    public class EffectivePermission
    {
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
        /// Source role name.
        /// </summary>
        public string SourceRole { get; set; } = "TenantAdmin";
    }
}
