namespace Xeno.Core.Models
{
    using System.Text.Json.Serialization;

    /// <summary>
    /// Request body for creating or updating a credential.
    /// </summary>
    public class CredentialSaveRequest
    {
        /// <summary>
        /// Tenant ID.
        /// </summary>
        [JsonPropertyName("tenantId")]
        public string? TenantId { get; set; } = null;

        /// <summary>
        /// User ID that owns the credential.
        /// </summary>
        [JsonPropertyName("userId")]
        public string? UserId { get; set; } = null;

        /// <summary>
        /// Access key.
        /// </summary>
        [JsonPropertyName("accessKey")]
        public string? AccessKey { get; set; } = null;

        /// <summary>
        /// Secret key.
        /// </summary>
        [JsonPropertyName("secretKey")]
        public string? SecretKey { get; set; } = null;

        /// <summary>
        /// Whether the credential is active.
        /// </summary>
        [JsonPropertyName("active")]
        public bool? Active { get; set; } = null;
    }
}
