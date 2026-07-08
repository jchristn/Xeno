namespace Xeno.Sdk
{
    using System.Text.Json.Serialization;

    /// <summary>
    /// Authenticated principal that requested an administrative nuke operation.
    /// </summary>
    public class AdminNukeRequestedBy
    {
        /// <summary>Caller tenant identifier.</summary>
        [JsonPropertyName("tenantId")]
        public string TenantId { get; set; } = string.Empty;

        /// <summary>Caller user identifier, when the caller is a user.</summary>
        [JsonPropertyName("userId")]
        public string? UserId { get; set; } = null;

        /// <summary>Whether the caller is a global/system administrator.</summary>
        [JsonPropertyName("isGlobalAdmin")]
        public bool IsGlobalAdmin { get; set; } = false;

        /// <summary>Whether the caller is a tenant administrator for the target tenant.</summary>
        [JsonPropertyName("isTenantAdmin")]
        public bool IsTenantAdmin { get; set; } = false;
    }
}
