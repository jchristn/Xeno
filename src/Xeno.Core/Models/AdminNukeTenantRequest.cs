namespace Xeno.Core.Models
{
    using System.Text.Json.Serialization;

    /// <summary>
    /// Administrative request to permanently remove a tenant and its tenant-owned data.
    /// </summary>
    public class AdminNukeTenantRequest
    {
        /// <summary>
        /// Tenant identifier to remove.
        /// </summary>
        [JsonPropertyName("tenantId")]
        public string? TenantId { get; set; } = null;

        /// <summary>
        /// Confirmation value that must exactly match <see cref="TenantId"/>.
        /// </summary>
        [JsonPropertyName("confirmTenantId")]
        public string? ConfirmTenantId { get; set; } = null;

        /// <summary>
        /// Whether to preview affected records without deleting them.
        /// </summary>
        [JsonPropertyName("dryRun")]
        public bool DryRun { get; set; } = true;

        /// <summary>
        /// Administrative reason for the destructive operation.
        /// </summary>
        [JsonPropertyName("reason")]
        public string? Reason { get; set; } = null;

        /// <summary>
        /// Whether tenant audit records should be deleted.
        /// </summary>
        [JsonPropertyName("includeAuditRecords")]
        public bool IncludeAuditRecords { get; set; } = true;

        /// <summary>
        /// Whether tenant request history should be deleted.
        /// </summary>
        [JsonPropertyName("includeRequestHistory")]
        public bool IncludeRequestHistory { get; set; } = true;

        /// <summary>
        /// Whether provider-specific tenant telemetry should be deleted.
        /// </summary>
        [JsonPropertyName("includeTelemetry")]
        public bool IncludeTelemetry { get; set; } = true;
    }
}
