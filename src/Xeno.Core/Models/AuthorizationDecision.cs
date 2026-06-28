namespace Xeno.Core.Models
{
    using Xeno.Core.Enums;

    /// <summary>
    /// Authorization decision for a protected operation.
    /// </summary>
    public class AuthorizationDecision
    {
        /// <summary>
        /// Whether the operation is allowed.
        /// </summary>
        public bool Allowed { get; set; } = false;

        /// <summary>
        /// HTTP status code that should be returned for this decision.
        /// </summary>
        public int StatusCode { get; set; } = 403;

        /// <summary>
        /// Machine-readable reason code.
        /// </summary>
        public string ReasonCode { get; set; } = "PermissionDenied";

        /// <summary>
        /// Human-readable decision message.
        /// </summary>
        public string Message { get; set; } = "Permission denied.";

        /// <summary>
        /// Tenant ID evaluated by the authorization service.
        /// </summary>
        public string TenantId { get; set; } = Constants.DefaultTenantId;

        /// <summary>
        /// Resource type evaluated by the authorization service.
        /// </summary>
        public string ResourceType { get; set; } = string.Empty;

        /// <summary>
        /// Operation type evaluated by the authorization service.
        /// </summary>
        public OperationTypeEnum OperationType { get; set; } = OperationTypeEnum.Read;

        /// <summary>
        /// Principal type evaluated by the authorization service.
        /// </summary>
        public PrincipalTypeEnum PrincipalType { get; set; } = PrincipalTypeEnum.None;

        /// <summary>
        /// Principal ID evaluated by the authorization service.
        /// </summary>
        public string? PrincipalId { get; set; } = null;

        /// <summary>
        /// Effective permission source used for the decision.
        /// </summary>
        public string? MatchedSource { get; set; } = null;
    }
}
