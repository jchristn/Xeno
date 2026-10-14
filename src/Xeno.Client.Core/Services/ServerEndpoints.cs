namespace Xeno.Client.Services
{
    using System;
    using System.Globalization;
    using Xeno.Core.Helpers;

    /// <summary>
    /// Resolves Xeno server REST endpoints from client settings. The REST base defaults to the WebSocket URL with the
    /// scheme mapped from ws/wss to http/https when no explicit REST URL is configured.
    /// </summary>
    public static class ServerEndpoints
    {
        /// <summary>
        /// Prefix used for generated client identifiers.
        /// </summary>
        public const string ClientIdPrefix = "xcl_";

        /// <summary>
        /// Build an absolute REST URI for a server path.
        /// </summary>
        /// <param name="settings">Client settings.</param>
        /// <param name="path">Server path, for example /v1.0/api/ping.</param>
        /// <returns>Absolute URI.</returns>
        /// <exception cref="ArgumentNullException">Settings is null.</exception>
        /// <exception cref="UriFormatException">The configured server URL is invalid.</exception>
        public static Uri BuildRestUri(ClientSettings settings, string path)
        {
            ArgumentNullException.ThrowIfNull(settings);
            UriBuilder builder = new UriBuilder(RestSource(settings));
            MapScheme(builder);
            builder.Path = (path ?? String.Empty).TrimStart('/');
            builder.Query = String.Empty;
            return builder.Uri;
        }

        /// <summary>
        /// Build the REST base URL (scheme, host, and port) without a trailing slash.
        /// </summary>
        /// <param name="settings">Client settings.</param>
        /// <returns>REST base URL.</returns>
        /// <exception cref="ArgumentNullException">Settings is null.</exception>
        /// <exception cref="UriFormatException">The configured server URL is invalid.</exception>
        public static string BuildRestBaseUrl(ClientSettings settings)
        {
            ArgumentNullException.ThrowIfNull(settings);
            UriBuilder builder = new UriBuilder(RestSource(settings));
            MapScheme(builder);
            builder.Path = String.Empty;
            builder.Query = String.Empty;
            return builder.Uri.ToString().TrimEnd('/');
        }

        /// <summary>
        /// Try to build the REST base URL, returning null instead of throwing for an invalid URL.
        /// </summary>
        /// <param name="settings">Client settings.</param>
        /// <returns>REST base URL, or null.</returns>
        public static string? TryBuildRestBaseUrl(ClientSettings? settings)
        {
            if (settings == null) return null;
            try
            {
                return BuildRestBaseUrl(settings);
            }
            catch (UriFormatException)
            {
                return null;
            }
        }

        /// <summary>
        /// Build a ping URI carrying the sequence number and client send time.
        /// </summary>
        /// <param name="endpoint">Ping endpoint.</param>
        /// <param name="sequence">Sequence number.</param>
        /// <param name="clientSentUtc">Client send time.</param>
        /// <returns>Ping URI.</returns>
        /// <exception cref="ArgumentNullException">Endpoint is null.</exception>
        public static Uri BuildPingUri(Uri endpoint, int sequence, DateTime clientSentUtc)
        {
            ArgumentNullException.ThrowIfNull(endpoint);
            UriBuilder builder = new UriBuilder(endpoint);
            string sequenceValue = sequence.ToString(CultureInfo.InvariantCulture);
            string sentValue = Uri.EscapeDataString(clientSentUtc.ToUniversalTime().ToString("O", CultureInfo.InvariantCulture));
            builder.Query = "sequence=" + sequenceValue + "&clientSentUtc=" + sentValue;
            return builder.Uri;
        }

        /// <summary>
        /// Determine whether the settings hold a credential that the WebSocket worker can authenticate with.
        /// </summary>
        /// <param name="settings">Client settings.</param>
        /// <returns>True when an access key is configured.</returns>
        public static bool HasWebSocketCredential(ClientSettings? settings)
        {
            return settings != null && !String.IsNullOrWhiteSpace(settings.Server.AccessKey);
        }

        /// <summary>
        /// Assign and save a client identifier when the settings do not have one.
        /// </summary>
        /// <param name="settings">Client settings.</param>
        /// <returns>Client identifier.</returns>
        /// <exception cref="ArgumentNullException">Settings is null.</exception>
        public static string EnsureClientId(ClientSettings settings)
        {
            ArgumentNullException.ThrowIfNull(settings);
            if (String.IsNullOrWhiteSpace(settings.ClientId))
            {
                settings.ClientId = IdGenerator.NewId(ClientIdPrefix);
                settings.Save();
            }

            return settings.ClientId;
        }

        /// <summary>
        /// Describe the configured server authentication for display.
        /// </summary>
        /// <param name="settings">Client settings.</param>
        /// <returns>Description.</returns>
        /// <exception cref="ArgumentNullException">Settings is null.</exception>
        public static string DescribeAuth(ClientSettings settings)
        {
            ArgumentNullException.ThrowIfNull(settings);
            if (!String.IsNullOrWhiteSpace(settings.Server.AccessKey)) return "access key " + settings.Server.AccessKey;
            if (!String.IsNullOrWhiteSpace(settings.Server.Token)) return "REST token only";
            return "not configured";
        }

        private static string RestSource(ClientSettings settings)
        {
            return String.IsNullOrWhiteSpace(settings.Server.RestUrl) ? settings.Server.Url : settings.Server.RestUrl;
        }

        private static void MapScheme(UriBuilder builder)
        {
            if (String.Equals(builder.Scheme, "ws", StringComparison.OrdinalIgnoreCase)) builder.Scheme = "http";
            if (String.Equals(builder.Scheme, "wss", StringComparison.OrdinalIgnoreCase)) builder.Scheme = "https";
        }
    }
}
