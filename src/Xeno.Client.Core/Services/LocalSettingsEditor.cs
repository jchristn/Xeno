namespace Xeno.Client.Services
{
    using System;
    using Xeno.Client.LocalDashboard.Models;

    /// <summary>
    /// Validates and applies edits to the local listener, history, and telemetry settings. Shared by the local
    /// dashboard settings API, the desktop app, and the terminal app so every surface enforces the same rules:
    /// the local inference server stays loopback-only, and the local dashboard accepts loopback or 0.0.0.0.
    /// </summary>
    public static class LocalSettingsEditor
    {
        #region Public-Methods

        /// <summary>
        /// Validate a local inference server host. Only localhost, 127.0.0.1, ::1, and [::1] are accepted.
        /// </summary>
        /// <param name="host">Host.</param>
        /// <exception cref="ArgumentException">The host is empty or not a loopback address.</exception>
        public static void ValidateLocalInferenceHost(string host)
        {
            if (String.IsNullOrWhiteSpace(host)) throw new ArgumentException("Local inference server host cannot be empty.", nameof(host));
            if (IsLoopback(host)) return;
            throw new ArgumentException("Local inference server host must be localhost, 127.0.0.1, ::1, or [::1]. Configured host: " + host, nameof(host));
        }

        /// <summary>
        /// Validate a local dashboard host. Loopback addresses and 0.0.0.0 are accepted; wildcards are rejected.
        /// </summary>
        /// <param name="host">Host.</param>
        /// <exception cref="ArgumentException">The host is empty, a wildcard, or not an accepted address.</exception>
        public static void ValidateLocalDashboardHost(string host)
        {
            if (String.IsNullOrWhiteSpace(host)) throw new ArgumentException("Local dashboard host cannot be empty.");
            if (String.Equals(host, "+", StringComparison.OrdinalIgnoreCase) || String.Equals(host, "*", StringComparison.OrdinalIgnoreCase))
            {
                throw new ArgumentException("Local dashboard host must be an explicit address such as 127.0.0.1 or 0.0.0.0.");
            }

            if (IsLoopback(host)) return;
            if (String.Equals(host, "0.0.0.0", StringComparison.OrdinalIgnoreCase)) return;
            throw new ArgumentException("Local dashboard host is not a valid host or IP address: " + host);
        }

        /// <summary>
        /// Determine whether a host is a loopback name or address.
        /// </summary>
        /// <param name="host">Host.</param>
        /// <returns>True for localhost, 127.0.0.1, ::1, and [::1].</returns>
        public static bool IsLoopback(string? host)
        {
            return String.Equals(host, "localhost", StringComparison.OrdinalIgnoreCase)
                || String.Equals(host, "127.0.0.1", StringComparison.OrdinalIgnoreCase)
                || String.Equals(host, "::1", StringComparison.OrdinalIgnoreCase)
                || String.Equals(host, "[::1]", StringComparison.OrdinalIgnoreCase);
        }

        /// <summary>
        /// Build an update request from the current settings, for editors that change a few fields.
        /// </summary>
        /// <param name="settings">Client settings.</param>
        /// <returns>Update request reflecting the current values.</returns>
        /// <exception cref="ArgumentNullException">Settings is null.</exception>
        public static LocalClientSettingsUpdateRequest FromSettings(ClientSettings settings)
        {
            ArgumentNullException.ThrowIfNull(settings);
            return new LocalClientSettingsUpdateRequest
            {
                Enabled = settings.LocalDashboard.Enabled,
                Host = settings.LocalDashboard.Host,
                Port = settings.LocalDashboard.Port,
                OpenBrowserOnStart = settings.LocalDashboard.OpenBrowserOnStart,
                RequireLocalToken = settings.LocalDashboard.RequireLocalToken,
                Database = settings.LocalDashboard.Database,
                HistoryRetention = settings.LocalDashboard.HistoryRetention,
                LocalInferenceServer = settings.LocalInferenceServer,
                Telemetry = settings.Telemetry
            };
        }

        /// <summary>
        /// Validate an update and apply it to the settings. The caller saves the settings; listener changes take
        /// effect after the client restarts.
        /// </summary>
        /// <param name="settings">Client settings to update.</param>
        /// <param name="request">Update.</param>
        /// <exception cref="ArgumentNullException">Settings or request is null.</exception>
        /// <exception cref="ArgumentException">A host is not allowed, or a provider endpoint would loop back to the local inference server.</exception>
        public static void Apply(ClientSettings settings, LocalClientSettingsUpdateRequest request)
        {
            ArgumentNullException.ThrowIfNull(settings);
            ArgumentNullException.ThrowIfNull(request);
            ValidateLocalDashboardHost(request.Host);
            ClientLocalInferenceServerSettings localInferenceServer = request.LocalInferenceServer ?? new ClientLocalInferenceServerSettings();
            ValidateLocalInferenceHost(localInferenceServer.Host);
            settings.ValidateLocalInferenceProviderEndpoints(localInferenceServer);
            settings.LocalDashboard.Enabled = request.Enabled;
            settings.LocalDashboard.Host = request.Host;
            settings.LocalDashboard.Port = Math.Max(1, request.Port);
            settings.LocalDashboard.OpenBrowserOnStart = request.OpenBrowserOnStart;
            settings.LocalDashboard.RequireLocalToken = request.RequireLocalToken;
            settings.LocalDashboard.Database = request.Database ?? new ClientLocalDatabaseSettings();
            settings.LocalDashboard.HistoryRetention = request.HistoryRetention ?? new ClientLocalHistorySettings();
            settings.LocalInferenceServer = localInferenceServer;
            settings.Telemetry = request.Telemetry ?? new ClientTelemetrySettings();
        }

        #endregion
    }
}
