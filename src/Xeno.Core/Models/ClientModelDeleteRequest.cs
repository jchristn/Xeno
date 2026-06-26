namespace Xeno.Core.Models
{
    using System.Text.Json.Serialization;

    /// <summary>
    /// Request body for deleting an advertised model from a client.
    /// </summary>
    public class ClientModelDeleteRequest
    {
        /// <summary>
        /// Client ID.
        /// </summary>
        [JsonPropertyName("clientId")]
        public string? ClientId { get; set; } = null;

        /// <summary>
        /// Model name.
        /// </summary>
        [JsonPropertyName("name")]
        public string? Name { get; set; } = null;

        /// <summary>
        /// Provider source name.
        /// </summary>
        [JsonPropertyName("source")]
        public string? Source { get; set; } = null;
    }
}
