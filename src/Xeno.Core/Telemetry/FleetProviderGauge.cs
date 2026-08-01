namespace Xeno.Core.Telemetry
{
    /// <summary>
    /// Latest provider-level state reported by a connected client.
    /// </summary>
    public class FleetProviderGauge
    {
        /// <summary>
        /// Client identifier.
        /// </summary>
        public string ClientId { get; set; } = string.Empty;

        /// <summary>
        /// Provider name (bounded set, e.g. ollama, vllm, openai).
        /// </summary>
        public string Provider { get; set; } = "unknown";

        /// <summary>
        /// Number of models advertised by this provider.
        /// </summary>
        public int AvailableModels { get; set; } = 0;

        /// <summary>
        /// Number of models currently loaded by this provider, or -1 if unreported.
        /// </summary>
        public int LoadedModels { get; set; } = -1;

        /// <summary>
        /// Whether the provider is reachable.
        /// </summary>
        public bool Up { get; set; } = true;

        /// <summary>
        /// Instantiate.
        /// </summary>
        public FleetProviderGauge()
        {
        }
    }
}
