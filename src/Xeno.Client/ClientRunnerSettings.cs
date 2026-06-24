namespace Xeno.Client
{
    using System;
    using System.Collections.Generic;
    using System.IO;
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
        /// Provider family.
        /// </summary>
        public string Provider { get; set; } = "Ollama";

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
