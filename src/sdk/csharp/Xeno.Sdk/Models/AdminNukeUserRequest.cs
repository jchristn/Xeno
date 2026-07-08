namespace Xeno.Sdk
{
    using System.Text.Json.Serialization;

    /// <summary>
    /// Administrative request to permanently remove a user and its user-owned data.
    /// </summary>
    public class AdminNukeUserRequest
    {
        /// <summary>
        /// Tenant that owns the user.
        /// </summary>
        [JsonPropertyName("tenantId")]
        public string? TenantId { get; set; } = null;

        /// <summary>
        /// User identifier to remove.
        /// </summary>
        [JsonPropertyName("userId")]
        public string? UserId { get; set; } = null;

        /// <summary>
        /// Confirmation value that must exactly match <see cref="UserId"/>.
        /// </summary>
        [JsonPropertyName("confirmUserId")]
        public string? ConfirmUserId { get; set; } = null;

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
        /// Whether user-related audit records should be deleted.
        /// </summary>
        [JsonPropertyName("includeAuditRecords")]
        public bool IncludeAuditRecords { get; set; } = false;

        /// <summary>
        /// Whether request history user references should be anonymized.
        /// </summary>
        [JsonPropertyName("anonymizeRequestHistory")]
        public bool AnonymizeRequestHistory { get; set; } = true;
    }
}
