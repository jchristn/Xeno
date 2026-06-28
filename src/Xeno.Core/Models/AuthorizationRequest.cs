namespace Xeno.Core.Models
{
    using Xeno.Core.Enums;

    /// <summary>
    /// Authorization request for a protected operation.
    /// </summary>
    public class AuthorizationRequest
    {
        /// <summary>
        /// Request context produced by authentication.
        /// </summary>
        public RequestContext Context { get; set; } = new RequestContext();

        /// <summary>
        /// Target tenant ID for the protected operation.
        /// </summary>
        public string TargetTenantId { get; set; } = Constants.DefaultTenantId;

        /// <summary>
        /// Resource type for the protected operation.
        /// </summary>
        public string ResourceType { get; set; } = string.Empty;

        /// <summary>
        /// Operation type for the protected operation.
        /// </summary>
        public OperationTypeEnum OperationType { get; set; } = OperationTypeEnum.Read;

        /// <summary>
        /// Optional target resource ID.
        /// </summary>
        public string? ResourceId { get; set; } = null;

        /// <summary>
        /// Whether system administrators may authorize the operation across tenants.
        /// </summary>
        public bool AllowSystemAdminCrossTenant { get; set; } = true;
    }
}
