namespace Xeno.Sdk
{
    using System.Text.Json.Serialization;

    /// <summary>
    /// Tenant details included in an administrative nuke response.
    /// </summary>
    public class AdminNukeTenantDetails
    {
        /// <summary>Tenant identifier.</summary>
        [JsonPropertyName("tenantId")]
        public string TenantId { get; set; } = string.Empty;

        /// <summary>Tenant display name.</summary>
        [JsonPropertyName("name")]
        public string Name { get; set; } = string.Empty;

        /// <summary>Tenant status for the response.</summary>
        [JsonPropertyName("status")]
        public string Status { get; set; } = string.Empty;
    }
}
