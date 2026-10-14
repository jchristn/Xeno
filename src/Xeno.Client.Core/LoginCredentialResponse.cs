namespace Xeno.Client
{
    using System.Text.Json.Serialization;

    /// <summary>
    /// Login response containing client credential material.
    /// </summary>
    internal sealed class LoginCredentialResponse
    {
        /// <summary>
        /// Access key issued by the Xeno server.
        /// </summary>
        [JsonPropertyName("accessKey")]
        public string? AccessKey { get; set; } = null;

        /// <summary>
        /// Tenant ID returned by the server.
        /// </summary>
        [JsonPropertyName("tenantId")]
        public string? TenantId { get; set; } = null;
    }
}
