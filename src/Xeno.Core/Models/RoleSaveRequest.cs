namespace Xeno.Core.Models
{
    using System.Collections.Generic;
    using System.Text.Json.Serialization;

    /// <summary>
    /// Request body for creating or updating a role.
    /// </summary>
    public class RoleSaveRequest
    {
        /// <summary>
        /// Role name.
        /// </summary>
        [JsonPropertyName("name")]
        public string? Name { get; set; } = null;

        /// <summary>
        /// Permission IDs attached to the role.
        /// </summary>
        [JsonPropertyName("permissionIds")]
        public List<string>? PermissionIds { get; set; } = null;

        /// <summary>
        /// Whether the role is active.
        /// </summary>
        [JsonPropertyName("active")]
        public bool? Active { get; set; } = null;
    }
}
