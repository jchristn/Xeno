namespace Xeno.Core.Models
{
    using System;
    using System.Collections.Generic;
    using System.Text.Json.Serialization;

    /// <summary>
    /// Provider source advertised by a Xeno client inventory message.
    /// </summary>
    public class ClientInventorySourceMessage
    {
        /// <summary>
        /// Client-local source name.
        /// </summary>
        [JsonPropertyName("name")]
        public string Name { get; set; } = "local";

        /// <summary>
        /// Provider family name.
        /// </summary>
        [JsonPropertyName("provider")]
        public string Provider { get; set; } = "Ollama";

        /// <summary>
        /// Models exposed through this source.
        /// </summary>
        [JsonPropertyName("models")]
        public List<ClientInventoryModelMessage> Models { get; set; } = new List<ClientInventoryModelMessage>();
    }
}
