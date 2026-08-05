namespace Xeno.Core.Models
{
    using System.Text.Json.Serialization;

    /// <summary>
    /// Request body for creating or updating a tenant.
    /// </summary>
    public class TenantSaveRequest
    {
        /// <summary>
        /// Tenant ID.
        /// </summary>
        [JsonPropertyName("id")]
        public string? Id { get; set; } = null;

        /// <summary>
        /// Tenant name.
        /// </summary>
        [JsonPropertyName("name")]
        public string? Name { get; set; } = null;

        /// <summary>
        /// Whether the tenant is active.
        /// </summary>
        [JsonPropertyName("active")]
        public bool? Active { get; set; } = null;

        /// <summary>
        /// Initial shared value applied to this tenant's clients when they first connect. When null the tenant's
        /// current value is left unchanged.
        /// </summary>
        [JsonPropertyName("defaultClientShared")]
        public bool? DefaultClientShared { get; set; } = null;

        /// <summary>
        /// Whether this tenant consumes shared clients owned by other tenants. When null the tenant's current
        /// value is left unchanged.
        /// </summary>
        [JsonPropertyName("consumeShared")]
        public bool? ConsumeShared { get; set; } = null;

        /// <summary>
        /// Whether subordinate service provisioning can be deferred when a service is unavailable.
        /// </summary>
        [JsonPropertyName("deferSubordinateProvisioning")]
        public bool? DeferSubordinateProvisioning { get; set; } = null;
    }
}
