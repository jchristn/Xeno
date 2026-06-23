namespace Xeno.Client
{
    using System;
    using System.Collections.Generic;
    using System.IO;
    using Xeno.Core.Serialization;

    /// <summary>
    /// Client runner settings.
    /// </summary>
    public class ClientRunnerSettings
    {
        /// <summary>
        /// Runner name.
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
        /// Models.
        /// </summary>
        public List<ClientModelSettings> Models { get; set; } = new List<ClientModelSettings>
        {
            new ClientModelSettings { Name = "llama3.2:latest" }
        };
    }
}
