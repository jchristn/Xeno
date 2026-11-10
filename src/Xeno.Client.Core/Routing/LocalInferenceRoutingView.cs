namespace Xeno.Client.Routing
{
    using System;
    using System.Collections.Generic;

    /// <summary>
    /// Everything the Local endpoint page shows, built once in <see cref="LocalInferenceRoutingService"/> for the CLI,
    /// the local web dashboard, the desktop app, the terminal app, and the tray agent.
    /// </summary>
    public class LocalInferenceRoutingView
    {
        /// <summary>
        /// Whether the listener is enabled in settings.
        /// </summary>
        public bool Enabled { get; set; } = false;

        /// <summary>
        /// Listener URL. Never null.
        /// </summary>
        public string Url { get; set; } = String.Empty;

        /// <summary>
        /// Routing mode. Never null.
        /// </summary>
        public string RoutingMode { get; set; } = String.Empty;

        /// <summary>
        /// Whether the running listener has published live status. False when the client or listener is not running.
        /// </summary>
        public bool RuntimeAvailable { get; set; } = false;

        /// <summary>
        /// When the live status was last written (UTC), when available.
        /// </summary>
        public DateTime? UpdatedUtc { get; set; } = null;

        /// <summary>
        /// Route rules. Never null.
        /// </summary>
        public List<LocalInferenceRouteRule> Rules { get; set; } = new List<LocalInferenceRouteRule>();

        /// <summary>
        /// Providers with health and concurrency. Never null.
        /// </summary>
        public List<LocalBackendView> Backends { get; set; } = new List<LocalBackendView>();

        /// <summary>
        /// Requests answered locally since the listener started.
        /// </summary>
        public long LocalRequests { get; set; } = 0;

        /// <summary>
        /// Requests sent to the network since the listener started.
        /// </summary>
        public long NetworkRequests { get; set; } = 0;

        /// <summary>
        /// Requests that fell back to the network since the listener started.
        /// </summary>
        public long Fallbacks { get; set; } = 0;

        /// <summary>
        /// Requests that ended with an error status since the listener started.
        /// </summary>
        public long Errors { get; set; } = 0;

        /// <summary>
        /// Default concurrency limit per provider.
        /// </summary>
        public int DefaultMaxConcurrentRequests { get; set; } = 4;

        /// <summary>
        /// Routable providers that are not unhealthy.
        /// </summary>
        public int HealthyBackends { get; set; } = 0;

        /// <summary>
        /// Routable providers.
        /// </summary>
        public int RoutableBackends { get; set; } = 0;
    }
}
