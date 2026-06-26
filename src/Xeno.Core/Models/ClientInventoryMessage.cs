namespace Xeno.Core.Models
{
    using System.Collections.Generic;
    using System.Text.Json.Serialization;

    /// <summary>
    /// Hardware and model inventory advertised by a Xeno client.
    /// </summary>
    public class ClientInventoryMessage
    {
        /// <summary>
        /// Models advertised directly by the client.
        /// </summary>
        [JsonPropertyName("models")]
        public List<ClientInventoryModelMessage> Models { get; set; } = new List<ClientInventoryModelMessage>();

        /// <summary>
        /// Provider endpoints advertised by the client.
        /// </summary>
        [JsonPropertyName("providers")]
        public List<ClientInventorySourceMessage> Providers { get; set; } = new List<ClientInventorySourceMessage>();

        /// <summary>
        /// Backward-compatible runner endpoint list.
        /// </summary>
        [JsonPropertyName("runners")]
        public List<ClientInventorySourceMessage> Runners { get; set; } = new List<ClientInventorySourceMessage>();
    }
}
