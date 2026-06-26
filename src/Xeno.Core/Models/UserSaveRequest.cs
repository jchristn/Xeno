namespace Xeno.Core.Models
{
    using System.Text.Json.Serialization;

    /// <summary>
    /// Request body for creating or updating a user.
    /// </summary>
    public class UserSaveRequest
    {
        /// <summary>
        /// Tenant ID.
        /// </summary>
        [JsonPropertyName("tenantId")]
        public string? TenantId { get; set; } = null;

        /// <summary>
        /// Username.
        /// </summary>
        [JsonPropertyName("username")]
        public string? Username { get; set; } = null;

        /// <summary>
        /// Email address.
        /// </summary>
        [JsonPropertyName("email")]
        public string? Email { get; set; } = null;

        /// <summary>
        /// Password.
        /// </summary>
        [JsonPropertyName("password")]
        public string? Password { get; set; } = null;

        /// <summary>
        /// Whether the user is a system administrator.
        /// </summary>
        [JsonPropertyName("isAdmin")]
        public bool? IsAdmin { get; set; } = null;

        /// <summary>
        /// Whether the user is a tenant administrator.
        /// </summary>
        [JsonPropertyName("isTenantAdmin")]
        public bool? IsTenantAdmin { get; set; } = null;

        /// <summary>
        /// Whether the user is active.
        /// </summary>
        [JsonPropertyName("active")]
        public bool? Active { get; set; } = null;
    }
}
