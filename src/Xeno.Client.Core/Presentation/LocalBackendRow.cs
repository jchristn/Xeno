namespace Xeno.Client.Presentation
{
    using System;
    using System.Globalization;
    using Xeno.Client.Routing;
    using Xeno.Client.Services;

    /// <summary>
    /// One local provider as the Local endpoint page lists it.
    /// </summary>
    public sealed class LocalBackendRow
    {
        /// <summary>
        /// Provider. Never null.
        /// </summary>
        public string Provider { get; set; } = String.Empty;

        /// <summary>
        /// API type. Never null.
        /// </summary>
        public string ApiType { get; set; } = String.Empty;

        /// <summary>
        /// Health label: "healthy", "unhealthy", "unknown", or "not used locally". Never null.
        /// </summary>
        public string Health { get; set; } = String.Empty;

        /// <summary>
        /// Latency text. Never null.
        /// </summary>
        public string Latency { get; set; } = String.Empty;

        /// <summary>
        /// Load text, for example "2 of 4 (1 local, 1 network)". Never null.
        /// </summary>
        public string Load { get; set; } = String.Empty;

        /// <summary>
        /// "yes" or "local only". Never null.
        /// </summary>
        public string Shared { get; set; } = String.Empty;

        /// <summary>
        /// Last error. Never null.
        /// </summary>
        public string LastError { get; set; } = String.Empty;

        /// <summary>
        /// The view.
        /// </summary>
        public LocalBackendView Source { get; set; } = new LocalBackendView();

        /// <summary>
        /// Build a row.
        /// </summary>
        /// <param name="view">Provider view.</param>
        /// <returns>Row.</returns>
        /// <exception cref="ArgumentNullException">view is null.</exception>
        public static LocalBackendRow From(LocalBackendView view)
        {
            ArgumentNullException.ThrowIfNull(view);
            string load = view.InFlight.ToString(CultureInfo.InvariantCulture) + " of " + view.Limit.ToString(CultureInfo.InvariantCulture);
            if (view.InFlight > 0) load += " (" + view.InFlightLocal.ToString(CultureInfo.InvariantCulture) + " local, " + view.InFlightNetwork.ToString(CultureInfo.InvariantCulture) + " network)";
            if (view.Queued > 0) load += ", " + view.Queued.ToString(CultureInfo.InvariantCulture) + " waiting";
            return new LocalBackendRow
            {
                Provider = view.Provider,
                ApiType = view.ApiType,
                Health = view.Health == "notRoutable" ? "not used locally" : view.Health,
                Latency = view.LatencyMs.HasValue ? DisplayFormat.Milliseconds(view.LatencyMs.Value) : DisplayFormat.Dash,
                Load = load,
                Shared = view.Shared ? "yes" : "local only",
                LastError = String.IsNullOrWhiteSpace(view.LastError) ? DisplayFormat.Dash : view.LastError,
                Source = view
            };
        }
    }
}
