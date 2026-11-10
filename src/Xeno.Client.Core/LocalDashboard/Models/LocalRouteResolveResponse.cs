namespace Xeno.Client.LocalDashboard.Models
{
    using System;

    /// <summary>
    /// Where the local inference listener would send a model, for the local dashboard API Explorer and Local endpoint page.
    /// </summary>
    public class LocalRouteResolveResponse
    {
        /// <summary>
        /// Model as asked. Never null.
        /// </summary>
        public string Model { get; set; } = String.Empty;

        /// <summary>
        /// Normalized model. Never null.
        /// </summary>
        public string NormalizedModel { get; set; } = String.Empty;

        /// <summary>
        /// "local", "network", or "unavailable". Never null.
        /// </summary>
        public string Target { get; set; } = String.Empty;

        /// <summary>
        /// X-Xeno-Route value the listener would return. Never null.
        /// </summary>
        public string Route { get; set; } = String.Empty;

        /// <summary>
        /// Local provider name, or empty. Never null.
        /// </summary>
        public string Provider { get; set; } = String.Empty;

        /// <summary>
        /// Stable reason code. Never null.
        /// </summary>
        public string Reason { get; set; } = String.Empty;

        /// <summary>
        /// English description of the reason. Front ends may translate <see cref="Reason"/> instead. Never null.
        /// </summary>
        public string ReasonText { get; set; } = String.Empty;

        /// <summary>
        /// Whether the request may fall back to the network.
        /// </summary>
        public bool FallbackToNetwork { get; set; } = false;

        /// <summary>
        /// Whether the provider accepts the asked API shape.
        /// </summary>
        public bool ShapeSupported { get; set; } = true;
    }
}
