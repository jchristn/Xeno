namespace Xeno.Client.Tools.Workspace.Web
{
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Net;
    using System.Net.Http;
    using System.Net.Sockets;
    using System.Threading;
    using System.Threading.Tasks;

    /// <summary>
    /// HTTP clients for the web tools. Without a test handler there are two: a public-only client whose connections
    /// go only to public addresses (checked for every address a host resolves to, so DNS cannot point it at a
    /// private one), and an unrestricted client for when private networks are allowed and for the search APIs. Neither
    /// follows redirects on its own or uses a proxy, so every hop is checked. Thread safe.
    /// </summary>
    internal sealed class WebHttp
    {
        #region Private-Members

        private readonly HttpClient? _Custom;
        private readonly Lazy<HttpClient> _PublicOnly;
        private readonly Lazy<HttpClient> _Unrestricted;

        #endregion

        #region Constructors-and-Factories

        /// <summary>
        /// Create the clients.
        /// </summary>
        /// <param name="handler">Handler for every request (tests), or null for real network clients.</param>
        public WebHttp(HttpMessageHandler? handler)
        {
            if (handler != null) _Custom = new HttpClient(handler, false) { Timeout = Timeout.InfiniteTimeSpan };
            _PublicOnly = new Lazy<HttpClient>(() => Build(true), LazyThreadSafetyMode.ExecutionAndPublication);
            _Unrestricted = new Lazy<HttpClient>(() => Build(false), LazyThreadSafetyMode.ExecutionAndPublication);
        }

        #endregion

        #region Public-Methods

        /// <summary>
        /// The client for a fetch.
        /// </summary>
        /// <param name="allowPrivate">Private networks are allowed.</param>
        /// <returns>Client.</returns>
        public HttpClient ForFetch(bool allowPrivate)
        {
            if (_Custom != null) return _Custom;
            return allowPrivate ? _Unrestricted.Value : _PublicOnly.Value;
        }

        /// <summary>
        /// The client for search provider APIs.
        /// </summary>
        /// <returns>Client.</returns>
        public HttpClient ForApi()
        {
            return _Custom ?? _Unrestricted.Value;
        }

        #endregion

        #region Private-Methods

        private static HttpClient Build(bool publicOnly)
        {
            SocketsHttpHandler handler = new SocketsHttpHandler
            {
                AllowAutoRedirect = false,
                UseProxy = false,
                UseCookies = false,
                AutomaticDecompression = DecompressionMethods.All,
                ConnectTimeout = TimeSpan.FromSeconds(15),
                PooledConnectionLifetime = TimeSpan.FromMinutes(2)
            };

            if (publicOnly) handler.ConnectCallback = ConnectPublicAsync;
            return new HttpClient(handler, true) { Timeout = Timeout.InfiniteTimeSpan };
        }

        private static async ValueTask<Stream> ConnectPublicAsync(SocketsHttpConnectionContext context, CancellationToken token)
        {
            string host = context.DnsEndPoint.Host;
            IPAddress[] resolved;
            if (IPAddress.TryParse(host.Trim('[', ']'), out IPAddress? literal)) resolved = new IPAddress[] { literal };
            else resolved = await Dns.GetHostAddressesAsync(host, token).ConfigureAwait(false);

            List<IPAddress> allowed = new List<IPAddress>();
            foreach (IPAddress address in resolved)
            {
                if (!WebAddressPolicy.IsNonPublic(address)) allowed.Add(address);
            }

            if (allowed.Count == 0) throw WebAddressPolicy.Refused(host);
            Socket socket = new Socket(SocketType.Stream, ProtocolType.Tcp) { NoDelay = true };
            try
            {
                await socket.ConnectAsync(allowed.ToArray(), context.DnsEndPoint.Port, token).ConfigureAwait(false);
                return new NetworkStream(socket, true);
            }
            catch
            {
                socket.Dispose();
                throw;
            }
        }

        #endregion
    }
}
