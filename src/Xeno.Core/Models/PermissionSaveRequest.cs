namespace Xeno.Core.Models
{
    using System.Text.Json.Serialization;

    /// <summary>
    /// Request body for creating or updating a permission.
    /// </summary>
    public class PermissionSaveRequest
    {
        /// <summary>
        /// Resource type covered by the permission.
        /// </summary>
        [JsonPropertyName("resourceType")]
        public string? ResourceType { get; set; } = null;

        /// <summary>
        /// Operation type covered by the permission.
        /// </summary>
        [JsonPropertyName("operationType")]
        public string? OperationType { get; set; } = null;

        /// <summary>
        /// Whether the permission grants or denies access.
        /// </summary>
        [JsonPropertyName("permit")]
        public bool? Permit { get; set; } = null;

        /// <summary>
        /// Whether the permission is active.
        /// </summary>
        [JsonPropertyName("active")]
        public bool? Active { get; set; } = null;
    }
}
