namespace Xeno.Client.Services
{
    using System.Collections.Generic;
    using System.Text.Json.Serialization;

    /// <summary>
    /// OpenAI-compatible model list returned by the Xeno server.
    /// </summary>
    public class NetworkModelListResponse
    {
        /// <summary>
        /// Object type, normally "list".
        /// </summary>
        [JsonPropertyName("object")]
        public string Object { get; set; } = "list";

        /// <summary>
        /// Models.
        /// </summary>
        [JsonPropertyName("data")]
        public List<NetworkModelEntry> Data { get; set; } = new List<NetworkModelEntry>();
    }
}
