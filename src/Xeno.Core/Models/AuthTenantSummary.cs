namespace Xeno.Core.Models
{
    using System.Text.Json.Serialization;

    /// <summary>
    /// Tenant summary used during dashboard login.
    /// </summary>
    public class AuthTenantSummary
    {
        /// <summary>
        /// Tenant ID.
        /// </summary>
        [JsonPropertyName("id")]
        public string Id { get; set; } = Constants.DefaultTenantId;

        /// <summary>
        /// Tenant display name.
        /// </summary>
        [JsonPropertyName("name")]
        public string Name { get; set; } = "Default";
    }
}
