namespace Test.Shared
{
    using System.Text.Json.Serialization;

    /// <summary>
    /// Login credential response projection used by WebSocket authentication tests.
    /// </summary>
    public class TestLoginCredentialResponse
    {
        /// <summary>
        /// Issued access key.
        /// </summary>
        [JsonPropertyName("accessKey")]
        public string? AccessKey { get; set; } = null;

    }
}
