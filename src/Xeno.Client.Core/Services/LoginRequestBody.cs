namespace Xeno.Client.Services
{
    using System;
    using System.Text.Json.Serialization;

    /// <summary>
    /// Body sent to the Xeno server login endpoint.
    /// </summary>
    internal sealed class LoginRequestBody
    {
        [JsonPropertyName("username")]
        public string Username { get; set; } = String.Empty;

        [JsonPropertyName("password")]
        public string Password { get; set; } = String.Empty;

        [JsonPropertyName("tenantId")]
        public string TenantId { get; set; } = String.Empty;

        [JsonPropertyName("clientId")]
        public string ClientId { get; set; } = String.Empty;

        [JsonPropertyName("label")]
        public string Label { get; set; } = String.Empty;
    }
}
