namespace Xeno.Core.Models
{
    using System.Text.Json.Serialization;

    /// <summary>
    /// Role assignment create or update request.
    /// </summary>
    public class RoleAssignmentSaveRequest
    {
        /// <summary>
        /// Optional tenant ID.
        /// </summary>
        [JsonPropertyName("tenantId")]
        public string? TenantId { get; set; } = null;

        /// <summary>
        /// Role ID to assign.
        /// </summary>
        [JsonPropertyName("roleId")]
        public string? RoleId { get; set; } = null;

        /// <summary>
        /// Principal type. Supported values are User and Credential.
        /// </summary>
        [JsonPropertyName("principalType")]
        public string? PrincipalType { get; set; } = null;

        /// <summary>
        /// Principal ID to receive the role.
        /// </summary>
        [JsonPropertyName("principalId")]
        public string? PrincipalId { get; set; } = null;

        /// <summary>
        /// Whether the assignment is active.
        /// </summary>
        [JsonPropertyName("active")]
        public bool? Active { get; set; } = null;
    }
}
