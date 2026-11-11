namespace Xeno.Client.Tools.Mcp
{
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Text.Json;
    using System.Threading;
    using System.Threading.Tasks;
    using Voltaic.Mcp;

    /// <summary>
    /// One live connection to an external MCP server: a stdio process (Voltaic <see cref="McpClient"/>) or a Streamable
    /// HTTP session (Voltaic <see cref="McpHttpClient"/>), with the tools it listed.
    /// </summary>
    internal sealed class McpClientConnection : IDisposable
    {
        #region Private-Members

        // Voltaic's stdio launcher starts the process with this process's environment and working directory, so a
        // server's environment and directory are set only while its process starts. Launches are serialized so one
        // server's values never reach another server, and the window is kept short by initializing after the launch.
        private static readonly SemaphoreSlim _LaunchGate = new SemaphoreSlim(1, 1);

        // The most tools/list pages followed for one server.
        private const int MaxToolListPages = 100;

        private static readonly JsonSerializerOptions _Json = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };

        private readonly McpClient? _Stdio;
        private readonly McpHttpClient? _Http;
        private int _Disposed = 0;

        #endregion

        #region Constructors-and-Factories

        private McpClientConnection(McpClient? stdio, McpHttpClient? http, string signature, int timeoutMs)
        {
            _Stdio = stdio;
            _Http = http;
            Signature = signature;
            TimeoutMs = timeoutMs;
        }

        #endregion

        #region Public-Members

        /// <summary>
        /// Settings the connection was made with, serialized, to notice changes.
        /// </summary>
        public string Signature { get; }

        /// <summary>
        /// Milliseconds one call may take.
        /// </summary>
        public int TimeoutMs { get; }

        /// <summary>
        /// Tools the server listed.
        /// </summary>
        public List<McpClientToolDefinition> Tools { get; private set; } = new List<McpClientToolDefinition>();

        /// <summary>
        /// True while the process runs or the session is open.
        /// </summary>
        public bool IsConnected
        {
            get
            {
                if (_Disposed != 0) return false;
                if (_Stdio != null) return _Stdio.IsConnected;
                return _Http != null && _Http.IsConnected;
            }
        }

        #endregion

        #region Public-Methods

        /// <summary>
        /// Connect to a server and list its tools, all within the server's timeout.
        /// </summary>
        /// <param name="settings">Server settings.</param>
        /// <param name="signature">Settings signature.</param>
        /// <param name="token">Cancellation token.</param>
        /// <returns>Connection.</returns>
        /// <exception cref="InvalidOperationException">The server could not be reached or did not list its tools.</exception>
        public static async Task<McpClientConnection> ConnectAsync(ChatMcpServerSettings settings, string signature, CancellationToken token)
        {
            ArgumentNullException.ThrowIfNull(settings);
            int timeoutMs = settings.TimeoutSeconds * 1000;
            string transport = NormalizeTransport(settings.Transport);
            McpClientConnection? connection = null;
            CancellationTokenSource bounded = CancellationTokenSource.CreateLinkedTokenSource(token);
            bounded.CancelAfter(timeoutMs);
            Task<McpClientConnection>? connect = null;
            try
            {
                connect = transport == "http"
                    ? ConnectHttpAsync(settings, signature, timeoutMs, bounded.Token)
                    : ConnectStdioAsync(settings, signature, timeoutMs, bounded.Token);
                connection = await connect.WaitAsync(TimeSpan.FromMilliseconds(timeoutMs), token).ConfigureAwait(false);
                await connection.ListToolsAsync(bounded.Token).ConfigureAwait(false);
                return connection;
            }
            catch (OperationCanceledException) when (token.IsCancellationRequested)
            {
                connection?.Dispose();
                throw;
            }
            catch (Exception ex) when (ex is TimeoutException || ex is OperationCanceledException)
            {
                connection?.Dispose();
                throw new InvalidOperationException("The server did not answer within " + settings.TimeoutSeconds + " seconds.", ex);
            }
            catch (Exception)
            {
                connection?.Dispose();
                throw;
            }
            finally
            {
                if (connect == null || connect.IsCompleted)
                {
                    bounded.Dispose();
                }
                else
                {
                    // The connect was abandoned at the timeout; close whatever it opens late.
                    _ = connect.ContinueWith(
                        (Task<McpClientConnection> late) =>
                        {
                            if (late.Status == TaskStatus.RanToCompletion) late.Result.Dispose();
                            bounded.Dispose();
                        },
                        CancellationToken.None,
                        TaskContinuationOptions.ExecuteSynchronously,
                        TaskScheduler.Default);
                }
            }
        }

        /// <summary>
        /// "stdio" or "http" for a configured transport.
        /// </summary>
        /// <param name="transport">Configured transport.</param>
        /// <returns>Normalized transport.</returns>
        /// <exception cref="InvalidOperationException">The transport is neither.</exception>
        public static string NormalizeTransport(string? transport)
        {
            string value = (transport ?? "stdio").Trim().ToLowerInvariant();
            if (value == "" || value == "stdio") return "stdio";
            if (value == "http" || value == "https" || value == "streamable-http" || value == "streamablehttp") return "http";
            throw new InvalidOperationException("Transport must be stdio or http, not " + transport + ".");
        }

        /// <summary>
        /// List the server's tools again, following pages.
        /// </summary>
        /// <param name="token">Cancellation token.</param>
        /// <returns>Task.</returns>
        public async Task ListToolsAsync(CancellationToken token)
        {
            List<McpClientToolDefinition> tools = new List<McpClientToolDefinition>();
            HashSet<string> seen = new HashSet<string>(StringComparer.Ordinal);
            string? cursor = null;
            int pages = 0;
            do
            {
                object? parameters = cursor == null ? null : new McpClientListToolsParams { Cursor = cursor };
                string json = await CallAsync("tools/list", parameters, token).ConfigureAwait(false);
                McpClientListToolsResult? page = JsonSerializer.Deserialize<McpClientListToolsResult>(json, _Json);
                pages++;
                cursor = page == null || String.IsNullOrEmpty(page.NextCursor) ? null : page.NextCursor;
                if (page?.Tools == null) continue;
                foreach (McpClientToolDefinition tool in page.Tools)
                {
                    // Skip malformed entries instead of dropping the whole list.
                    if (tool == null || String.IsNullOrWhiteSpace(tool.Name) || !seen.Add(tool.Name)) continue;
                    tools.Add(tool);
                }
            }
            while (cursor != null && pages < MaxToolListPages);

            Tools = tools;
        }

        /// <summary>
        /// Call a method and return its result as JSON text.
        /// </summary>
        /// <param name="method">Method.</param>
        /// <param name="parameters">Parameters, or null.</param>
        /// <param name="token">Cancellation token.</param>
        /// <returns>Result JSON.</returns>
        public async Task<string> CallAsync(string method, object? parameters, CancellationToken token)
        {
            // Results arrive as untyped JSON and are serialized back to text for the named result types.
            object? result;
            if (_Stdio != null) result = await _Stdio.CallAsync<object>(method, parameters, TimeoutMs, token).ConfigureAwait(false);
            else if (_Http != null) result = await _Http.CallAsync<object>(method, parameters, TimeoutMs, token).ConfigureAwait(false);
            else throw new InvalidOperationException("The connection is closed.");
            return result == null ? "{}" : JsonSerializer.Serialize(result);
        }

        /// <summary>
        /// Stop the stdio process or close the HTTP session.
        /// </summary>
        public void Dispose()
        {
            if (Interlocked.Exchange(ref _Disposed, 1) != 0) return;
            if (_Stdio != null)
            {
                try { _Stdio.Shutdown(); } catch (Exception) { }
                try { _Stdio.Dispose(); } catch (Exception) { }
            }

            if (_Http != null)
            {
                try { _Http.Disconnect(); } catch (Exception) { }
                try { _Http.Dispose(); } catch (Exception) { }
            }
        }

        #endregion

        #region Private-Methods

        private static async Task<McpClientConnection> ConnectStdioAsync(ChatMcpServerSettings settings, string signature, int timeoutMs, CancellationToken token)
        {
            if (String.IsNullOrWhiteSpace(settings.Command)) throw new InvalidOperationException("A stdio server needs a command.");
            string directory = String.IsNullOrWhiteSpace(settings.WorkingDirectory)
                ? ClientSettings.SettingsDirectory
                : Path.GetFullPath(Environment.ExpandEnvironmentVariables(settings.WorkingDirectory));
            if (!Directory.Exists(directory)) throw new InvalidOperationException("The working directory " + directory + " does not exist.");

            McpClient client = new McpClient();
            client.ClientName = "Xeno";
            client.ClientVersion = Xeno.Client.Hosting.XenoProduct.Version;
            client.AutoInitialize = false;
            client.InitializeTimeoutMs = Math.Clamp(timeoutMs, 100, 600000);
            McpClientLogCapture log = new McpClientLogCapture();
            client.Log += log.Capture;
            try
            {
                bool launched;
                await _LaunchGate.WaitAsync(token).ConfigureAwait(false);
                Dictionary<string, string?> previous = new Dictionary<string, string?>(StringComparer.Ordinal);
                string previousDirectory = Directory.GetCurrentDirectory();
                bool changedDirectory = false;
                try
                {
                    foreach (KeyValuePair<string, string> variable in settings.Environment ?? new Dictionary<string, string>())
                    {
                        if (String.IsNullOrEmpty(variable.Key)) continue;
                        if (!previous.ContainsKey(variable.Key)) previous[variable.Key] = Environment.GetEnvironmentVariable(variable.Key);
                        Environment.SetEnvironmentVariable(variable.Key, Environment.ExpandEnvironmentVariables(variable.Value ?? String.Empty));
                    }

                    if (!String.Equals(previousDirectory, directory, StringComparison.Ordinal))
                    {
                        Directory.SetCurrentDirectory(directory);
                        changedDirectory = true;
                    }

                    List<string> arguments = settings.Arguments ?? new List<string>();
                    launched = await client.LaunchServerAsync(settings.Command, arguments.ToArray(), token).ConfigureAwait(false);
                }
                finally
                {
                    if (changedDirectory)
                    {
                        try { Directory.SetCurrentDirectory(previousDirectory); } catch (Exception) { }
                    }

                    foreach (KeyValuePair<string, string?> variable in previous) Environment.SetEnvironmentVariable(variable.Key, variable.Value);
                    _LaunchGate.Release();
                }

                if (!launched)
                {
                    // Give the stderr reader a moment to deliver the server's last words.
                    await Task.Delay(150, CancellationToken.None).ConfigureAwait(false);
                    throw new InvalidOperationException("Could not start " + settings.Command + "." + log.Describe());
                }

                try
                {
                    await client.InitializeAsync(Math.Clamp(timeoutMs, 100, 600000), token).ConfigureAwait(false);
                }
                catch (Exception ex) when (ex is not OperationCanceledException)
                {
                    throw new InvalidOperationException(settings.Command + " started but did not complete the MCP handshake: " + ex.Message + log.Describe(), ex);
                }

                client.Log -= log.Capture;
                return new McpClientConnection(client, null, signature, timeoutMs);
            }
            catch (Exception)
            {
                client.Log -= log.Capture;
                try { client.Shutdown(); } catch (Exception) { }
                try { client.Dispose(); } catch (Exception) { }
                throw;
            }
        }

        private static async Task<McpClientConnection> ConnectHttpAsync(ChatMcpServerSettings settings, string signature, int timeoutMs, CancellationToken token)
        {
            if (!Uri.TryCreate(settings.Url, UriKind.Absolute, out Uri? uri) || (uri.Scheme != Uri.UriSchemeHttp && uri.Scheme != Uri.UriSchemeHttps))
            {
                throw new InvalidOperationException("An http server needs an absolute http:// or https:// URL.");
            }

            string baseUrl = uri.GetLeftPart(UriPartial.Authority);
            string path = uri.PathAndQuery;
            if (String.IsNullOrEmpty(path) || path == "/") path = "/mcp";
            string display = uri.GetLeftPart(UriPartial.Path);

            McpHttpClient client = new McpHttpClient();
            client.ClientName = "Xeno";
            client.ClientVersion = Xeno.Client.Hosting.XenoProduct.Version;
            client.RequestTimeoutMs = Math.Max(1000, timeoutMs);
            McpClientLogCapture log = new McpClientLogCapture();
            client.Log += log.Capture;
            try
            {
                foreach (KeyValuePair<string, string> header in settings.Headers ?? new Dictionary<string, string>())
                {
                    if (String.IsNullOrWhiteSpace(header.Key)) continue;
                    client.SetRequestHeader(header.Key, Environment.ExpandEnvironmentVariables(header.Value ?? String.Empty));
                }

                bool connected = await client.ConnectStreamableAsync(baseUrl, path, token).ConfigureAwait(false);
                token.ThrowIfCancellationRequested();
                if (!connected) throw new InvalidOperationException("Could not connect to " + display + "." + log.Describe());
                client.Log -= log.Capture;
                return new McpClientConnection(null, client, signature, timeoutMs);
            }
            catch (Exception)
            {
                client.Log -= log.Capture;
                try { client.Dispose(); } catch (Exception) { }
                throw;
            }
        }

        #endregion
    }
}
