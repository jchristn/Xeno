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
        private int? _MaxConcurrentRequests = null;

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
        /// Whether the Xeno server is allowed to manage Ollama models for this provider endpoint.
        /// </summary>
        public bool AllowModelManagement { get; set; } = false;

        /// <summary>
        /// Upstream request timeout in milliseconds.
        /// </summary>
        public int TimeoutMs { get; set; } = 300000;

        /// <summary>
        /// Whether this provider's models are offered to the Xeno network. Default is true. Set false for a local-only provider, such as a personal Ollama Cloud or ollama.com account.
        /// </summary>
        public bool ShareWithNetwork { get; set; } = true;

        /// <summary>
        /// Whether the local inference listener may route this machine's requests directly to this provider. Default is true.
        /// </summary>
        public bool RouteLocalRequests { get; set; } = true;

        /// <summary>
        /// Maximum concurrent requests across all models on this provider, shared by network and local requests. Null (the default) inherits ClientSettings.DefaultMaxConcurrentRequests. Minimum 1, maximum 256; out-of-range values are clamped.
        /// </summary>
        public int? MaxConcurrentRequests
        {
            get
            {
                return _MaxConcurrentRequests;
            }
            set
            {
                _MaxConcurrentRequests = value.HasValue ? Math.Min(256, Math.Max(1, value.Value)) : null;
            }
        }

        /// <summary>
        /// Models.
        /// </summary>
        public List<ClientModelSettings> Models { get; set; } = new List<ClientModelSettings>
        {
            new ClientModelSettings { Name = "llama3.2:latest" }
        };
    }
}
