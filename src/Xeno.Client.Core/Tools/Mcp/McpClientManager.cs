namespace Xeno.Client.Tools.Mcp
{
    using System;
    using System.Collections.Generic;
    using System.Text.Json;
    using System.Threading;
    using System.Threading.Tasks;

    /// <summary>
    /// Connects to the external MCP servers in <see cref="ClientChatSettings.McpServers"/> (stdio processes and
    /// Streamable HTTP endpoints, through Voltaic), offers their tools to chat as mcp_&lt;server&gt;_&lt;tool&gt;, and
    /// calls them. Tool arguments and results are never logged. Thread safe.
    /// </summary>
    public sealed class McpClientManager : IMcpToolSource, IDisposable
    {
        #region Private-Members

        private static readonly JsonSerializerOptions _Json = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };

        private readonly Func<ClientSettings> _Settings;
        private readonly object _Lock = new object();
        private readonly SemaphoreSlim _Gate = new SemaphoreSlim(1, 1);
        private readonly Dictionary<string, McpClientConnection> _Connections = new Dictionary<string, McpClientConnection>(StringComparer.OrdinalIgnoreCase);
        private readonly Dictionary<string, string> _Errors = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        private List<ChatMcpServerSettings> _Configured = new List<ChatMcpServerSettings>();
        private IReadOnlyList<McpChatTool> _Tools = new List<McpChatTool>();
        private Dictionary<string, McpChatTool> _ToolsByName = new Dictionary<string, McpChatTool>(StringComparer.Ordinal);
        private IReadOnlyList<McpServerStatus> _Servers = new List<McpServerStatus>();
        private bool _Disposed = false;

        #endregion

        #region Constructors-and-Factories

        /// <summary>
        /// Create the manager. Nothing connects until <see cref="RefreshAsync"/>.
        /// </summary>
        /// <param name="settings">Reads the current client settings.</param>
        /// <exception cref="ArgumentNullException">settings is null.</exception>
        public McpClientManager(Func<ClientSettings> settings)
        {
            _Settings = settings ?? throw new ArgumentNullException(nameof(settings));
        }

        #endregion

        #region Public-Members

        /// <inheritdoc/>
        public IReadOnlyList<McpChatTool> Tools
        {
            get
            {
                lock (_Lock) return _Tools;
            }
        }

        /// <inheritdoc/>
        public IReadOnlyList<McpServerStatus> Servers
        {
            get
            {
                lock (_Lock)
                {
                    List<McpServerStatus> copy = new List<McpServerStatus>(_Servers.Count);
                    foreach (McpServerStatus status in _Servers)
                    {
                        copy.Add(new McpServerStatus
                        {
                            Name = status.Name,
                            Transport = status.Transport,
                            Enabled = status.Enabled,
                            Connected = status.Connected,
                            ToolCount = status.ToolCount,
                            Error = status.Error
                        });
                    }

                    return copy;
                }
            }
        }

        #endregion

        #region Public-Methods

        /// <inheritdoc/>
        public async Task RefreshAsync(CancellationToken token)
        {
            ObjectDisposedException.ThrowIf(_Disposed, this);
            await _Gate.WaitAsync(token).ConfigureAwait(false);
            try
            {
                List<ChatMcpServerSettings> configured = ReadServers();
                Dictionary<string, ChatMcpServerSettings> wanted = Connectable(configured);
                List<McpClientConnection> closing = new List<McpClientConnection>();
                Dictionary<string, McpClientConnection> current = new Dictionary<string, McpClientConnection>(StringComparer.OrdinalIgnoreCase);
                lock (_Lock)
                {
                    foreach (KeyValuePair<string, McpClientConnection> entry in new List<KeyValuePair<string, McpClientConnection>>(_Connections))
                    {
                        // Removed, disabled, or changed in settings: close it; changed servers reconnect below.
                        if (!wanted.TryGetValue(entry.Key, out ChatMcpServerSettings? server) || !String.Equals(Signature(server), entry.Value.Signature, StringComparison.Ordinal))
                        {
                            closing.Add(entry.Value);
                            _Connections.Remove(entry.Key);
                        }
                        else
                        {
                            current[entry.Key] = entry.Value;
                        }
                    }

                    foreach (string name in new List<string>(_Errors.Keys))
                    {
                        if (!wanted.ContainsKey(name)) _Errors.Remove(name);
                    }

                    _Configured = configured;
                    Rebuild();
                }

                foreach (McpClientConnection connection in closing) connection.Dispose();

                List<Task<McpClientRefreshOutcome>> tasks = new List<Task<McpClientRefreshOutcome>>();
                foreach (ChatMcpServerSettings server in wanted.Values)
                {
                    current.TryGetValue(server.Name, out McpClientConnection? existing);
                    tasks.Add(RefreshServerAsync(server, existing, token));
                }

                McpClientRefreshOutcome[] outcomes = await Task.WhenAll(tasks).ConfigureAwait(false);
                List<McpClientConnection> orphans = new List<McpClientConnection>();
                lock (_Lock)
                {
                    foreach (McpClientRefreshOutcome outcome in outcomes)
                    {
                        if (_Connections.TryGetValue(outcome.Name, out McpClientConnection? previous) && !ReferenceEquals(previous, outcome.Connection)) orphans.Add(previous);
                        if (outcome.Connection != null && !_Disposed)
                        {
                            _Connections[outcome.Name] = outcome.Connection;
                            _Errors.Remove(outcome.Name);
                        }
                        else
                        {
                            if (outcome.Connection != null) orphans.Add(outcome.Connection);
                            _Connections.Remove(outcome.Name);
                            if (outcome.Error != null) _Errors[outcome.Name] = outcome.Error;
                        }
                    }

                    Rebuild();
                }

                foreach (McpClientConnection orphan in orphans) orphan.Dispose();
                token.ThrowIfCancellationRequested();
            }
            finally
            {
                _Gate.Release();
            }
        }

        /// <inheritdoc/>
        public async Task<string> InvokeAsync(string name, string arguments, CancellationToken token)
        {
            ObjectDisposedException.ThrowIf(_Disposed, this);
            if (String.IsNullOrWhiteSpace(name)) throw new ArgumentException("A tool name is required.", nameof(name));
            McpChatTool? tool;
            McpClientConnection? connection;
            lock (_Lock)
            {
                if (!_ToolsByName.TryGetValue(name, out tool)) throw new ArgumentException("There is no MCP tool named " + name + ".", nameof(name));
                _Connections.TryGetValue(tool.Server, out connection);
            }

            McpClientCallParams parameters = new McpClientCallParams { Name = tool.ToolName, Arguments = ParseArguments(arguments) };
            bool reconnected = false;
            if (connection == null || !connection.IsConnected)
            {
                connection = await ReconnectAsync(tool.Server, connection, token).ConfigureAwait(false);
                reconnected = true;
            }

            string json;
            try
            {
                json = await connection.CallAsync("tools/call", parameters, token).ConfigureAwait(false);
            }
            catch (OperationCanceledException) when (token.IsCancellationRequested)
            {
                throw;
            }
            catch (Exception) when (!reconnected && !connection.IsConnected)
            {
                // The connection dropped (for example the server process exited): reconnect once and retry.
                connection = await ReconnectAsync(tool.Server, connection, token).ConfigureAwait(false);
                try
                {
                    json = await connection.CallAsync("tools/call", parameters, token).ConfigureAwait(false);
                }
                catch (OperationCanceledException) when (token.IsCancellationRequested)
                {
                    throw;
                }
                catch (Exception retry)
                {
                    throw CallFailure(tool, connection, retry);
                }
            }
            catch (Exception ex)
            {
                throw CallFailure(tool, connection, ex);
            }

            McpClientCallResult? result;
            try
            {
                result = JsonSerializer.Deserialize<McpClientCallResult>(json, _Json);
            }
            catch (JsonException)
            {
                throw new InvalidOperationException("MCP server " + tool.Server + " returned a result that is not a tool result.");
            }

            string text = result?.ToText() ?? String.Empty;
            if (result?.IsError == true)
            {
                throw new InvalidOperationException(String.IsNullOrWhiteSpace(text) ? "MCP tool " + tool.ToolName + " on " + tool.Server + " reported an error." : text);
            }

            return text;
        }

        /// <summary>
        /// Close every connection and stop stdio server processes.
        /// </summary>
        public void Dispose()
        {
            List<McpClientConnection> connections;
            lock (_Lock)
            {
                if (_Disposed) return;
                _Disposed = true;
                connections = new List<McpClientConnection>(_Connections.Values);
                _Connections.Clear();
                _Tools = new List<McpChatTool>();
                _ToolsByName = new Dictionary<string, McpChatTool>(StringComparer.Ordinal);
            }

            foreach (McpClientConnection connection in connections) connection.Dispose();
        }

        #endregion

        #region Private-Methods

        private List<ChatMcpServerSettings> ReadServers()
        {
            ClientSettings settings = _Settings() ?? new ClientSettings();
            List<ChatMcpServerSettings> servers = new List<ChatMcpServerSettings>();
            if (settings.Chat?.McpServers == null) return servers;
            foreach (ChatMcpServerSettings server in settings.Chat.McpServers)
            {
                if (server != null) servers.Add(server);
            }

            return servers;
        }

        // Enabled servers with a usable name, first definition of a name winning.
        private static Dictionary<string, ChatMcpServerSettings> Connectable(List<ChatMcpServerSettings> configured)
        {
            Dictionary<string, ChatMcpServerSettings> wanted = new Dictionary<string, ChatMcpServerSettings>(StringComparer.OrdinalIgnoreCase);
            HashSet<string> seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (ChatMcpServerSettings server in configured)
            {
                if (String.IsNullOrWhiteSpace(server.Name) || !seen.Add(server.Name)) continue;
                if (server.Enabled) wanted[server.Name] = server;
            }

            return wanted;
        }

        private static async Task<McpClientRefreshOutcome> RefreshServerAsync(ChatMcpServerSettings server, McpClientConnection? existing, CancellationToken token)
        {
            McpClientRefreshOutcome outcome = new McpClientRefreshOutcome { Name = server.Name };
            try
            {
                McpClientConnection.NormalizeTransport(server.Transport);
            }
            catch (InvalidOperationException ex)
            {
                existing?.Dispose();
                outcome.Error = ex.Message;
                return outcome;
            }

            if (existing != null && existing.IsConnected)
            {
                try
                {
                    using (CancellationTokenSource bounded = CancellationTokenSource.CreateLinkedTokenSource(token))
                    {
                        bounded.CancelAfter(existing.TimeoutMs);
                        await existing.ListToolsAsync(bounded.Token).ConfigureAwait(false);
                    }

                    outcome.Connection = existing;
                    return outcome;
                }
                catch (OperationCanceledException) when (token.IsCancellationRequested)
                {
                    outcome.Connection = existing;
                    return outcome;
                }
                catch (Exception)
                {
                    // Fall through to a fresh connection.
                }
            }

            existing?.Dispose();
            try
            {
                outcome.Connection = await McpClientConnection.ConnectAsync(server, Signature(server), token).ConfigureAwait(false);
            }
            catch (OperationCanceledException) when (token.IsCancellationRequested)
            {
                outcome.Error = "Connecting was cancelled.";
            }
            catch (Exception ex)
            {
                outcome.Error = ex.Message;
            }

            return outcome;
        }

        private async Task<McpClientConnection> ReconnectAsync(string serverName, McpClientConnection? stale, CancellationToken token)
        {
            await _Gate.WaitAsync(token).ConfigureAwait(false);
            try
            {
                ChatMcpServerSettings? server = null;
                McpClientConnection? current;
                lock (_Lock)
                {
                    ObjectDisposedException.ThrowIf(_Disposed, this);
                    _Connections.TryGetValue(serverName, out current);
                    foreach (ChatMcpServerSettings candidate in _Configured)
                    {
                        if (candidate.Enabled && String.Equals(candidate.Name, serverName, StringComparison.OrdinalIgnoreCase))
                        {
                            server = candidate;
                            break;
                        }
                    }

                    // Another call or a refresh already reconnected.
                    if (current != null && !ReferenceEquals(current, stale) && current.IsConnected) return current;
                    if (current != null) _Connections.Remove(serverName);
                }

                if (server == null) throw new InvalidOperationException("MCP server " + serverName + " is no longer configured.");
                current?.Dispose();
                McpClientConnection connection;
                try
                {
                    connection = await McpClientConnection.ConnectAsync(server, Signature(server), token).ConfigureAwait(false);
                }
                catch (OperationCanceledException) when (token.IsCancellationRequested)
                {
                    lock (_Lock) Rebuild();
                    throw;
                }
                catch (Exception ex)
                {
                    lock (_Lock)
                    {
                        _Errors[serverName] = ex.Message;
                        Rebuild();
                    }

                    throw new InvalidOperationException("MCP server " + serverName + " is not connected: " + ex.Message, ex);
                }

                bool disposed;
                lock (_Lock)
                {
                    disposed = _Disposed;
                    if (!disposed)
                    {
                        _Connections[serverName] = connection;
                        _Errors.Remove(serverName);
                        Rebuild();
                    }
                }

                if (disposed)
                {
                    connection.Dispose();
                    throw new ObjectDisposedException(nameof(McpClientManager));
                }

                return connection;
            }
            finally
            {
                _Gate.Release();
            }
        }

        // Rebuild the offered tools and server states from the connections. Call holding _Lock.
        private void Rebuild()
        {
            List<McpChatTool> tools = new List<McpChatTool>();
            Dictionary<string, McpChatTool> byName = new Dictionary<string, McpChatTool>(StringComparer.Ordinal);
            HashSet<string> used = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            HashSet<string> seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            List<McpServerStatus> statuses = new List<McpServerStatus>();
            foreach (ChatMcpServerSettings server in _Configured)
            {
                McpServerStatus status = new McpServerStatus
                {
                    Name = server.Name ?? String.Empty,
                    Transport = TransportLabel(server.Transport),
                    Enabled = server.Enabled
                };
                statuses.Add(status);
                if (String.IsNullOrWhiteSpace(server.Name))
                {
                    if (server.Enabled) status.Error = "The server needs a name.";
                    continue;
                }

                if (!seen.Add(server.Name))
                {
                    if (server.Enabled) status.Error = "Another server already uses the name " + server.Name + ".";
                    continue;
                }

                if (!server.Enabled) continue;
                if (!_Connections.TryGetValue(server.Name, out McpClientConnection? connection))
                {
                    _Errors.TryGetValue(server.Name, out string? error);
                    status.Error = error;
                    continue;
                }

                status.Connected = connection.IsConnected;
                status.ToolCount = connection.Tools.Count;
                foreach (McpClientToolDefinition definition in connection.Tools)
                {
                    string toolName = definition.Name ?? String.Empty;
                    bool readOnly = definition.Annotations?.ReadOnlyHint == true;
                    McpChatTool tool = new McpChatTool
                    {
                        Name = McpClientToolNames.Build(server.Name, toolName, used),
                        Server = server.Name,
                        ToolName = toolName,
                        Description = !String.IsNullOrWhiteSpace(definition.Description) ? definition.Description : definition.Title ?? String.Empty,
                        Schema = ConvertSchema(definition.InputSchema),
                        Mutating = !(server.TrustReadOnlyTools || readOnly)
                    };
                    tools.Add(tool);
                    byName[tool.Name] = tool;
                }
            }

            _Tools = tools;
            _ToolsByName = byName;
            _Servers = statuses;
        }

        // Schemas arrive as raw JSON; serialize them back to text and read the parts chat needs into named types.
        private static Xeno.Core.Chat.Wire.ChatToolSchemaDto ConvertSchema(object? inputSchema)
        {
            if (inputSchema == null) return new Xeno.Core.Chat.Wire.ChatToolSchemaDto();
            try
            {
                string json = JsonSerializer.Serialize(inputSchema);
                McpClientJsonSchema? schema = JsonSerializer.Deserialize<McpClientJsonSchema>(json, _Json);
                return schema?.ToToolSchema() ?? new Xeno.Core.Chat.Wire.ChatToolSchemaDto();
            }
            catch (JsonException)
            {
                return new Xeno.Core.Chat.Wire.ChatToolSchemaDto();
            }
            catch (InvalidOperationException)
            {
                return new Xeno.Core.Chat.Wire.ChatToolSchemaDto();
            }
        }

        private static Dictionary<string, object?> ParseArguments(string arguments)
        {
            if (String.IsNullOrWhiteSpace(arguments)) return new Dictionary<string, object?>(StringComparer.Ordinal);
            try
            {
                Dictionary<string, object?>? parsed = JsonSerializer.Deserialize<Dictionary<string, object?>>(arguments, _Json);
                return parsed ?? new Dictionary<string, object?>(StringComparer.Ordinal);
            }
            catch (JsonException)
            {
                throw new ArgumentException("Tool arguments must be a JSON object.", nameof(arguments));
            }
        }

        private static Exception CallFailure(McpChatTool tool, McpClientConnection connection, Exception ex)
        {
            if (ex is TimeoutException || ex is OperationCanceledException)
            {
                return new InvalidOperationException("MCP tool " + tool.ToolName + " on " + tool.Server + " did not answer within " + (connection.TimeoutMs / 1000) + " seconds.", ex);
            }

            return new InvalidOperationException("MCP tool " + tool.ToolName + " on " + tool.Server + " failed: " + ex.Message, ex);
        }

        private static string TransportLabel(string? transport)
        {
            try
            {
                return McpClientConnection.NormalizeTransport(transport);
            }
            catch (InvalidOperationException)
            {
                return transport ?? String.Empty;
            }
        }

        private static string Signature(ChatMcpServerSettings server)
        {
            return JsonSerializer.Serialize(server);
        }

        #endregion
    }
}
