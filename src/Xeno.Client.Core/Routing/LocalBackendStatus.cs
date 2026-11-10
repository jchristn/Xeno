namespace Xeno.Client.Routing
{
    using System;
    using System.Collections.Generic;

    /// <summary>
    /// Health and discovered models for one local provider, as last seen by <see cref="LocalBackendHealthMonitor"/>.
    /// Instances are immutable snapshots once published by the monitor.
    /// </summary>
    public class LocalBackendStatus
    {
        /// <summary>
        /// Provider name. Never null.
        /// </summary>
        public string Provider { get; set; } = String.Empty;

        /// <summary>
        /// Provider API type, for example "Ollama" or "vLLM". Never null.
        /// </summary>
        public string ApiType { get; set; } = String.Empty;

        /// <summary>
        /// Provider endpoint. Never null.
        /// </summary>
        public string Endpoint { get; set; } = String.Empty;

        /// <summary>
        /// True when the last check succeeded, false when it failed, null before the first check.
        /// </summary>
        public bool? Healthy { get; set; } = null;

        /// <summary>
        /// Stable health code for front ends: "unknown", "healthy", or "unhealthy". Never null.
        /// </summary>
        public string HealthCode
        {
            get
            {
                if (!Healthy.HasValue) return "unknown";
                return Healthy.Value ? "healthy" : "unhealthy";
            }
        }

        /// <summary>
        /// When the provider was last checked (UTC). Null before the first check.
        /// </summary>
        public DateTime? LastCheckedUtc { get; set; } = null;

        /// <summary>
        /// Latency of the last successful check in milliseconds. Null when unknown.
        /// </summary>
        public double? LastLatencyMs { get; set; } = null;

        /// <summary>
        /// Error from the last failed check or request. Empty when healthy. Never null.
        /// </summary>
        public string LastError { get; set; } = String.Empty;

        /// <summary>
        /// Model names the provider reported in its last successful check. Never null.
        /// </summary>
        public List<string> DiscoveredModels { get; set; } = new List<string>();
    }
}
