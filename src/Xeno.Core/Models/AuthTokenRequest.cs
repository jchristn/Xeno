namespace Xeno.Core.Models
{
    using System.Text.Json.Serialization;

    /// <summary>
    /// Credential token request body.
    /// </summary>
    public class AuthTokenRequest
    {
        /// <summary>
        /// Access key.
        /// </summary>
        [JsonPropertyName("accessKey")]
        public string? AccessKey { get; set; } = null;

        /// <summary>
        /// Legacy secret key. Supplying this value is rejected.
        /// </summary>
        [JsonPropertyName("secretKey")]
        public string? SecretKey { get; set; } = null;
    }
}
