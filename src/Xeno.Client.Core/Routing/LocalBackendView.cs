namespace Xeno.Client.Routing
{
    using System;

    /// <summary>
    /// One local provider as every front end shows it: settings, health, and concurrency together.
    /// </summary>
    public class LocalBackendView
    {
        /// <summary>
        /// Provider name. Never null.
        /// </summary>
        public string Provider { get; set; } = String.Empty;

        /// <summary>
        /// API type. Never null.
        /// </summary>
        public string ApiType { get; set; } = String.Empty;

        /// <summary>
        /// Endpoint. Never null.
        /// </summary>
        public string Endpoint { get; set; } = String.Empty;

        /// <summary>
        /// Stable health code: "unknown", "healthy", "unhealthy", or "notRoutable". Never null.
        /// </summary>
        public string Health { get; set; } = "unknown";

        /// <summary>
        /// When the provider was last checked (UTC), when known.
        /// </summary>
        public DateTime? LastCheckedUtc { get; set; } = null;

        /// <summary>
        /// Last health check latency in milliseconds, when known.
        /// </summary>
        public double? LatencyMs { get; set; } = null;

        /// <summary>
        /// Last health or request error. Never null.
        /// </summary>
        public string LastError { get; set; } = String.Empty;

        /// <summary>
        /// Requests holding a slot.
        /// </summary>
        public int InFlight { get; set; } = 0;

        /// <summary>
        /// Local requests holding a slot.
        /// </summary>
        public int InFlightLocal { get; set; } = 0;

        /// <summary>
        /// Network requests holding a slot.
        /// </summary>
        public int InFlightNetwork { get; set; } = 0;

        /// <summary>
        /// Local requests waiting for a slot.
        /// </summary>
        public int Queued { get; set; } = 0;

        /// <summary>
        /// Provider concurrency limit in effect.
        /// </summary>
        public int Limit { get; set; } = 0;

        /// <summary>
        /// Where the limit comes from: "provider" or "global". Never null.
        /// </summary>
        public string LimitSource { get; set; } = "global";

        /// <summary>
        /// Whether the provider is shared with the Xeno network.
        /// </summary>
        public bool Shared { get; set; } = true;

        /// <summary>
        /// Whether the listener may route to the provider.
        /// </summary>
        public bool Routable { get; set; } = true;

        /// <summary>
        /// Models the provider reported in its last check.
        /// </summary>
        public int DiscoveredModels { get; set; } = 0;
    }
}
