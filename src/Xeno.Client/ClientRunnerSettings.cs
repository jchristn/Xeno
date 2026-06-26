namespace Xeno.Client
{
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Text.Json.Serialization;
    using Xeno.Core.Serialization;

    /// <summary>
    /// Provider endpoint settings.
    /// </summary>
    public class ClientRunnerSettings
    {
        /// <summary>
        /// Provider endpoint name.
        /// </summary>
        public string Name { get; set; } = "local-ollama";

        /// <summary>
        /// Upstream API type.
        /// </summary>
        public string ApiType { get; set; } = "Ollama";

        /// <summary>
        /// Backward-compatible upstream API type alias.
        /// </summary>
        [JsonPropertyName("Provider")]
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
        /// Endpoint URL.
        /// </summary>
        public string Endpoint { get; set; } = "http://127.0.0.1:11434";

        /// <summary>
        /// Optional upstream credential.
        /// </summary>
        public string? Credential { get; set; } = null;

        /// <summary>
        /// Upstream request timeout in milliseconds.
        /// </summary>
        public int TimeoutMs { get; set; } = 300000;

        /// <summary>
        /// Models.
        /// </summary>
        public List<ClientModelSettings> Models { get; set; } = new List<ClientModelSettings>
        {
            new ClientModelSettings { Name = "llama3.2:latest" }
        };
    }
}
