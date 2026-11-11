namespace Xeno.Client.Mcp
{
    using System;
    using System.Net;
    using System.Threading;
    using System.Threading.Tasks;
    using Voltaic.Core;
    using Voltaic.Mcp;
    using Xeno.Client.LocalInference;
    using Xeno.Client.Tools;

    /// <summary>
    /// Runs the client's MCP server over HTTP (Streamable HTTP at /mcp, JSON-RPC at /rpc) on a loopback port while the
    /// client runs, when <see cref="ClientMcpSettings.Enabled"/> is on. Requests must carry the token from
    /// <see cref="XenoMcpToken"/> unless <see cref="ClientMcpSettings.RequireToken"/> is off.
    /// </summary>
    public static class XenoMcpHttpRuntime
    {
        #region Private-Members

        private static readonly object _Lock = new object();
        private static McpHttpServer? _Server;
        private static CancellationTokenSource? _Cancellation;
        private static Xeno.Client.Services.XenoWorkspace? _FallbackWorkspace;

        #endregion

        #region Public-Members

        /// <summary>
        /// URL of the running server, or null.
        /// </summary>
        public static string? Url { get; private set; } = null;

        #endregion

        #region Public-Methods

        /// <summary>
        /// Start the server when enabled, with the local dashboard's tool catalog, or with one of its own when the local
        /// dashboard is off (then xeno_chat is not offered, because chat runs in the local dashboard service).
        /// </summary>
        /// <param name="settings">Client settings.</param>
        /// <param name="catalog">Tool catalog, or null to build one.</param>
        /// <param name="token">Cancellation token.</param>
        /// <returns>True when the server is listening.</returns>
        /// <exception cref="ArgumentNullException">settings is null.</exception>
        public static async Task<bool> StartAsync(ClientSettings settings, XenoToolCatalog? catalog, CancellationToken token)
        {
            ArgumentNullException.ThrowIfNull(settings);
            if (settings.Mcp == null || !settings.Mcp.Enabled) return false;
            if (catalog == null)
            {
                Xeno.Client.Chat.SqliteChatStore store = new Xeno.Client.Chat.SqliteChatStore(settings.LocalDashboard.Database);
                await store.InitializeAsync(token).ConfigureAwait(false);
                Xeno.Client.Services.XenoWorkspace workspace = new Xeno.Client.Services.XenoWorkspace();
                lock (_Lock) _FallbackWorkspace = workspace;
                catalog = new XenoToolCatalog(workspace, cancel => Task.FromResult<Xeno.Core.Chat.IChatStore>(store), null);
            }

            return Start(settings, catalog);
        }

        /// <summary>
        /// Start the server when enabled.
        /// </summary>
        /// <param name="settings">Client settings.</param>
        /// <param name="catalog">Tool catalog.</param>
        /// <returns>True when the server is listening.</returns>
        /// <exception cref="ArgumentNullException">settings or catalog is null.</exception>
        public static bool Start(ClientSettings settings, XenoToolCatalog catalog)
        {
            ArgumentNullException.ThrowIfNull(settings);
            ArgumentNullException.ThrowIfNull(catalog);
            ClientMcpSettings mcp = settings.Mcp ?? new ClientMcpSettings();
            if (!mcp.Enabled) return false;
            try
            {
                try
                {
                    LocalInferenceServerService.ValidateLoopbackHost(mcp.Host);
                }
                catch (ArgumentException)
                {
                    throw new ArgumentException("The MCP server host must be localhost, 127.0.0.1, ::1, or [::1]. Configured host: " + mcp.Host + ".");
                }

                string host = mcp.Host.Trim('[', ']');
                McpHttpServer server = new McpHttpServer(host, mcp.Port);
                server.RestrictToLoopbackClients = true;
                XenoMcpTools.Register(server, catalog);
                if (mcp.RequireToken)
                {
                    string token = XenoMcpToken.GetOrCreate();
                    server.AuthenticationHandler = request => Task.FromResult(Authenticate(request, token));
                }

                CancellationTokenSource cancellation = new CancellationTokenSource();
                Task listen = server.StartAsync(cancellation.Token);
                if (listen.IsCompleted)
                {
                    Exception? cause = listen.Exception?.GetBaseException();
                    cancellation.Dispose();
                    Console.Error.WriteLine("MCP server could not listen on " + mcp.Url() + ": " + (cause?.Message ?? "the listener stopped immediately") + ".");
                    return false;
                }

                _ = listen.ContinueWith(task =>
                {
                    if (task.IsFaulted && !cancellation.IsCancellationRequested) Console.Error.WriteLine("MCP server stopped: " + task.Exception?.GetBaseException().Message);
                }, TaskScheduler.Default);

                lock (_Lock)
                {
                    _Server = server;
                    _Cancellation = cancellation;
                    Url = mcp.Url();
                }

                Console.WriteLine("MCP server listening at " + mcp.Url() + " (Streamable HTTP at /mcp).");
                return true;
            }
            catch (Exception ex) when (ex is ArgumentException || ex is HttpListenerException || ex is System.Net.Sockets.SocketException || ex is InvalidOperationException || ex is System.IO.IOException)
            {
                Console.Error.WriteLine("MCP server could not start: " + ex.Message);
                return false;
            }
        }

        /// <summary>
        /// Stop the server.
        /// </summary>
        public static void Stop()
        {
            McpHttpServer? server;
            CancellationTokenSource? cancellation;
            lock (_Lock)
            {
                server = _Server;
                cancellation = _Cancellation;
                _Server = null;
                _Cancellation = null;
                Url = null;
                _FallbackWorkspace?.Dispose();
                _FallbackWorkspace = null;
            }

            if (server == null) return;
            try
            {
                cancellation?.Cancel();
                server.Stop();
                server.Dispose();
            }
            catch (Exception ex) when (ex is ObjectDisposedException || ex is InvalidOperationException || ex is HttpListenerException)
            {
            }
            finally
            {
                cancellation?.Dispose();
            }

            Console.WriteLine("MCP server stopped.");
        }

        /// <summary>
        /// Check a request's MCP token.
        /// </summary>
        /// <param name="request">Request.</param>
        /// <param name="token">Expected token.</param>
        /// <returns>Result.</returns>
        public static AuthenticationResult Authenticate(HttpListenerRequest request, string token)
        {
            ArgumentNullException.ThrowIfNull(request);
            string? header = request.Headers["Authorization"];
            string? presented = header != null && header.StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase) ? header.Substring(7).Trim() : request.Headers["X-Xeno-Mcp-Token"];
            if (XenoMcpToken.Matches(presented, token)) return new AuthenticationResult { IsAuthenticated = true, Principal = "xeno-mcp" };
            return new AuthenticationResult { IsAuthenticated = false, StatusCode = 401, ErrorMessage = "Send the token from 'xeno mcp token' as Authorization: Bearer <token>." };
        }

        #endregion
    }
}
