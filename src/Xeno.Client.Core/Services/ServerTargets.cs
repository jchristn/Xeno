namespace Xeno.Client.Services
{
    using System;
    using System.Globalization;

    /// <summary>
    /// Resolves the Xeno server a client points at. The default is the Xeno cloud server. A different server is
    /// chosen with a WebSocket URL (ws:// or wss://), a REST URL (http:// or https://), or a bare host[:port], so the
    /// CLI, the desktop app, and the terminal app accept the same values.
    /// </summary>
    public static class ServerTargets
    {
        /// <summary>
        /// Xeno cloud server host.
        /// </summary>
        public const string CloudHost = "app.xenocloud.ai";

        /// <summary>
        /// Xeno cloud server WebSocket URL, the default for new clients.
        /// </summary>
        public const string CloudWebSocketUrl = "wss://app.xenocloud.ai:9000/ws/v1/clients";

        /// <summary>
        /// WebSocket path that Xeno servers accept client connections on.
        /// </summary>
        public const string WebSocketPath = "/ws/v1/clients";

        /// <summary>
        /// Port used when a bare host is given without one.
        /// </summary>
        public const int DefaultPort = 9000;

        /// <summary>
        /// Keyword that selects the Xeno cloud server.
        /// </summary>
        public const string CloudKeyword = "cloud";

        /// <summary>
        /// Resolve a server value. Accepts "cloud", a ws:// or wss:// URL (the client path is added when missing), an
        /// http:// or https:// REST URL (the WebSocket URL is derived from it), or a bare host[:port] (port 9000 when
        /// omitted).
        /// </summary>
        /// <param name="value">Server value.</param>
        /// <returns>Resolved target.</returns>
        /// <exception cref="ArgumentException">The value is empty or not a valid server URL.</exception>
        public static ServerTarget Resolve(string? value)
        {
            if (String.IsNullOrWhiteSpace(value)) throw new ArgumentException("A server URL is required.");
            string trimmed = value.Trim();
            if (String.Equals(trimmed, CloudKeyword, StringComparison.OrdinalIgnoreCase) || String.Equals(trimmed, "default", StringComparison.OrdinalIgnoreCase))
            {
                return Describe(CloudWebSocketUrl, null);
            }

            bool bareHost = !trimmed.Contains("://", StringComparison.Ordinal);
            string authority = bareHost ? trimmed.Split('/')[0] : String.Empty;
            bool barePort = bareHost && authority.LastIndexOf(':') > authority.LastIndexOf(']');
            if (bareHost) trimmed = "http://" + trimmed;
            if (!Uri.TryCreate(trimmed, UriKind.Absolute, out Uri? uri) || String.IsNullOrWhiteSpace(uri.Host))
            {
                throw new ArgumentException("'" + value.Trim() + "' is not a server URL. Use cloud, a ws:// or wss:// URL, an http:// or https:// URL, or host[:port].");
            }

            string scheme = uri.Scheme.ToLowerInvariant();
            if (scheme == "ws" || scheme == "wss")
            {
                UriBuilder ws = new UriBuilder(uri) { Query = String.Empty, Fragment = String.Empty };
                if (String.IsNullOrEmpty(uri.AbsolutePath) || uri.AbsolutePath == "/") ws.Path = WebSocketPath;
                return Describe(ws.Uri.ToString(), null);
            }

            if (scheme == "http" || scheme == "https")
            {
                UriBuilder ws = new UriBuilder(uri)
                {
                    Scheme = scheme == "https" ? "wss" : "ws",
                    Path = WebSocketPath,
                    Query = String.Empty,
                    Fragment = String.Empty,
                    Port = bareHost && !barePort ? DefaultPort : uri.Port
                };

                return Describe(ws.Uri.ToString(), null);
            }

            throw new ArgumentException("Unsupported server URL scheme '" + uri.Scheme + "'. Use ws, wss, http, or https.");
        }

        /// <summary>
        /// Upgrade a plain ws:// or http:// Xeno cloud address saved by older clients to wss:// and https://. The Xeno
        /// cloud only accepts TLS, so the old default can neither sign in nor connect.
        /// </summary>
        /// <param name="settings">Client settings, changed in memory only.</param>
        /// <returns>True when the settings changed.</returns>
        /// <exception cref="ArgumentNullException">Settings is null.</exception>
        public static bool UpgradeInsecureCloudUrls(ClientSettings settings)
        {
            ArgumentNullException.ThrowIfNull(settings);
            bool changed = false;
            string? upgradedSocket = UpgradeCloud(settings.Server.Url, "ws", "wss");
            if (upgradedSocket != null)
            {
                settings.Server.Url = upgradedSocket;
                changed = true;
            }

            string? upgradedRest = UpgradeCloud(settings.Server.RestUrl, "http", "https");
            if (upgradedRest != null)
            {
                settings.Server.RestUrl = upgradedRest;
                changed = true;
            }

            return changed;
        }

        /// <summary>
        /// Apply the server named at sign-in. When it is a different server from the one configured, the client is
        /// pointed at it entirely (WebSocket and REST), so the worker connects to the server that issued the
        /// credential. When it is the configured server, the settings are left unchanged.
        /// </summary>
        /// <param name="settings">Client settings, changed in memory only.</param>
        /// <param name="serverUrl">Server value from the sign-in form or --server, or null to keep the configured server.</param>
        /// <returns>True when the settings changed.</returns>
        /// <exception cref="ArgumentNullException">Settings is null.</exception>
        /// <exception cref="ArgumentException">The server value is invalid.</exception>
        public static bool ApplySignInServer(ClientSettings settings, string? serverUrl)
        {
            ArgumentNullException.ThrowIfNull(settings);
            if (String.IsNullOrWhiteSpace(serverUrl)) return false;
            ServerTarget target = Resolve(serverUrl);
            ClientSettings candidate = new ClientSettings();
            candidate.Server.Url = target.WebSocketUrl;
            string? wanted = ServerEndpoints.TryBuildRestBaseUrl(candidate);
            string? current = ServerEndpoints.TryBuildRestBaseUrl(settings);
            if (wanted != null && String.Equals(wanted, current, StringComparison.OrdinalIgnoreCase)) return false;
            settings.Server.Url = target.WebSocketUrl;
            settings.Server.RestUrl = target.RestUrl;
            return true;
        }

        /// <summary>
        /// Describe the server configured in client settings.
        /// </summary>
        /// <param name="settings">Client settings.</param>
        /// <returns>Target.</returns>
        /// <exception cref="ArgumentNullException">Settings is null.</exception>
        public static ServerTarget FromSettings(ClientSettings settings)
        {
            ArgumentNullException.ThrowIfNull(settings);
            return Describe(settings.Server.Url ?? String.Empty, settings.Server.RestUrl);
        }

        /// <summary>
        /// Short label for a server: "Xeno cloud" for the cloud server, otherwise host and port.
        /// </summary>
        /// <param name="webSocketUrl">WebSocket URL.</param>
        /// <param name="restUrl">Explicit REST URL, or null.</param>
        /// <returns>Label, or an empty string for an invalid URL.</returns>
        public static string Label(string? webSocketUrl, string? restUrl)
        {
            string? source = String.IsNullOrWhiteSpace(restUrl) ? webSocketUrl : restUrl;
            if (String.IsNullOrWhiteSpace(source) || !Uri.TryCreate(source, UriKind.Absolute, out Uri? uri)) return String.Empty;
            if (String.Equals(uri.Host, CloudHost, StringComparison.OrdinalIgnoreCase)) return "Xeno cloud";
            return uri.IsDefaultPort ? uri.Host : uri.Host + ":" + uri.Port.ToString(CultureInfo.InvariantCulture);
        }

        private static string? UpgradeCloud(string? url, string insecureScheme, string secureScheme)
        {
            if (String.IsNullOrWhiteSpace(url) || !Uri.TryCreate(url, UriKind.Absolute, out Uri? uri)) return null;
            if (!String.Equals(uri.Host, CloudHost, StringComparison.OrdinalIgnoreCase)) return null;
            if (!String.Equals(uri.Scheme, insecureScheme, StringComparison.OrdinalIgnoreCase)) return null;
            string trimmed = url.Trim();
            return secureScheme + trimmed.Substring(insecureScheme.Length);
        }

        private static ServerTarget Describe(string webSocketUrl, string? restUrl)
        {
            bool cloud = Uri.TryCreate(webSocketUrl, UriKind.Absolute, out Uri? uri) && String.Equals(uri.Host, CloudHost, StringComparison.OrdinalIgnoreCase);
            return new ServerTarget
            {
                WebSocketUrl = webSocketUrl,
                RestUrl = String.IsNullOrWhiteSpace(restUrl) ? null : restUrl,
                IsCloud = cloud,
                Label = Label(webSocketUrl, restUrl)
            };
        }
    }
}
