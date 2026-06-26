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
        /// Upstream API type name.
        /// </summary>
        [JsonPropertyName("apiType")]
        public string ApiType { get; set; } = "Ollama";

        /// <summary>
        /// Backward-compatible upstream API type alias.
        /// </summary>
        [JsonPropertyName("provider")]
        public string? LegacyProvider
        {
            get
            {
                return null;
            }
            set
            {
                if (!String.IsNullOrWhiteSpace(value)) ApiType = value;
            }
        }

        /// <summary>
        /// Models exposed through this source.
        /// </summary>
        [JsonPropertyName("models")]
        public List<ClientInventoryModelMessage> Models { get; set; } = new List<ClientInventoryModelMessage>();
    }
}
