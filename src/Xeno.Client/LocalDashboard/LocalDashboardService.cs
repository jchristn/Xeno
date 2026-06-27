namespace Xeno.Client.LocalDashboard
{
    using System;
    using System.Collections.Generic;
    using System.Globalization;
    using System.IO;
    using System.Linq;
    using System.Net;
    using System.Net.Http;
    using System.Text;
    using System.Threading;
    using System.Threading.Tasks;
    using WatsonWebserver;
    using WatsonWebserver.Core;
    using Xeno.Client.LocalDashboard.Database;
    using Xeno.Client.LocalDashboard.Models;
    using Xeno.Client.Telemetry.Models;
    using Xeno.Client.Telemetry.Services;
    using Xeno.Core.Serialization;

    /// <summary>
    /// Watson-backed local dashboard service for the Xeno client.
    /// </summary>
    public sealed class LocalDashboardService : IDisposable
    {
        private readonly ClientSettings _Settings;
        private readonly ILocalRequestHistoryMethods _History;
        private readonly IClientTelemetryHistoryMethods _TelemetryHistory;
        private readonly ClientTelemetryPersistenceService? _TelemetryService;
        private readonly Webserver _Webserver;
        private readonly string _Token;
        private bool _Disposed;

        /// <summary>
        /// Instantiate the local dashboard service.
        /// </summary>
        /// <param name="settings">Client settings.</param>
        /// <param name="history">Local history database methods.</param>
        /// <param name="telemetryHistory">Telemetry history database methods.</param>
        /// <param name="telemetryService">Telemetry persistence service.</param>
        public LocalDashboardService(ClientSettings settings, ILocalRequestHistoryMethods history, IClientTelemetryHistoryMethods telemetryHistory, ClientTelemetryPersistenceService? telemetryService)
        {
            _Settings = settings ?? throw new ArgumentNullException(nameof(settings));
            _History = history ?? throw new ArgumentNullException(nameof(history));
            _TelemetryHistory = telemetryHistory ?? throw new ArgumentNullException(nameof(telemetryHistory));
            _TelemetryService = telemetryService;
            ValidateHost(_Settings.LocalDashboard.Host);
            WebserverSettings webserverSettings = new WebserverSettings(_Settings.LocalDashboard.Host, _Settings.LocalDashboard.Port, false);
            _Webserver = new Webserver(webserverSettings, DefaultRouteAsync);
            _Webserver.Routes.Preflight = PreflightRouteAsync;
            _Token = Guid.NewGuid().ToString("N");
        }

        /// <summary>
        /// Local dashboard URL.
        /// </summary>
        public string DashboardUrl
        {
            get
            {
                string host = String.Equals(_Settings.LocalDashboard.Host, "0.0.0.0", StringComparison.OrdinalIgnoreCase) ? "127.0.0.1" : _Settings.LocalDashboard.Host;
                return "http://" + host + ":" + _Settings.LocalDashboard.Port.ToString(CultureInfo.InvariantCulture);
            }
        }

        /// <summary>
        /// Start the local dashboard service.
        /// </summary>
        /// <param name="token">Cancellation token.</param>
        /// <returns>Task.</returns>
        public Task StartAsync(CancellationToken token)
        {
            token.ThrowIfCancellationRequested();
            _Webserver.Start();
            return Task.CompletedTask;
        }

        /// <summary>
        /// Stop the local dashboard service.
        /// </summary>
        public void Stop()
        {
            try
            {
                _Webserver.Stop();
            }
            catch (InvalidOperationException ex) when (ex.Message.Contains("already stopped", StringComparison.OrdinalIgnoreCase))
            {
            }
        }

        /// <summary>
        /// Dispose resources.
        /// </summary>
        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        private static void ValidateHost(string host)
        {
            if (String.IsNullOrWhiteSpace(host)) throw new ArgumentException("Local dashboard host cannot be empty.");
            if (String.Equals(host, "+", StringComparison.OrdinalIgnoreCase) || String.Equals(host, "*", StringComparison.OrdinalIgnoreCase))
            {
                throw new ArgumentException("Local dashboard host must be an explicit address such as 127.0.0.1 or 0.0.0.0.");
            }

            if (String.Equals(host, "localhost", StringComparison.OrdinalIgnoreCase)) return;
            if (String.Equals(host, "127.0.0.1", StringComparison.OrdinalIgnoreCase)) return;
            if (String.Equals(host, "::1", StringComparison.OrdinalIgnoreCase)) return;
            if (String.Equals(host, "[::1]", StringComparison.OrdinalIgnoreCase)) return;
            if (String.Equals(host, "0.0.0.0", StringComparison.OrdinalIgnoreCase)) return;
            throw new ArgumentException("Local dashboard host is not a valid host or IP address: " + host);
        }

        private async Task PreflightRouteAsync(HttpContextBase context)
        {
            ApplyHeaders(context);
            ApplyCorsHeaders(context);
            context.Response.StatusCode = 204;
            await context.Response.Send(context.Token).ConfigureAwait(false);
        }

        private async Task DefaultRouteAsync(HttpContextBase context)
        {
            string path = context.Request.Url.RawWithoutQuery ?? "/";
            string method = context.Request.Method.ToString().ToUpperInvariant();
            try
            {
                ApplyHeaders(context);
                ApplyCorsHeaders(context);
                if (method == "GET" && (String.Equals(path, "/", StringComparison.OrdinalIgnoreCase) || String.Equals(path, "/index.html", StringComparison.OrdinalIgnoreCase)))
                {
                    if (LocalDashboardAssetService.TryGetAsset(path, _Token, out LocalDashboardAsset asset))
                    {
                        await SendAssetAsync(context, asset).ConfigureAwait(false);
                        return;
                    }

                    await SendHtmlAsync(context, DashboardHtml()).ConfigureAwait(false);
                    return;
                }

                if (method == "GET" && path.StartsWith("/assets/", StringComparison.OrdinalIgnoreCase))
                {
                    if (LocalDashboardAssetService.TryGetAsset(path, _Token, out LocalDashboardAsset asset))
                    {
                        await SendAssetAsync(context, asset).ConfigureAwait(false);
                        return;
                    }

                    context.Response.StatusCode = 404;
                    await SendJsonAsync(context, new LocalDashboardErrorResponse { Error = "NotFound", Message = "Asset not found." }).ConfigureAwait(false);
                    return;
                }

                if (method == "GET" && IsRootAsset(path))
                {
                    if (LocalDashboardAssetService.TryGetAsset(path, _Token, out LocalDashboardAsset asset))
                    {
                        await SendAssetAsync(context, asset).ConfigureAwait(false);
                        return;
                    }

                    context.Response.StatusCode = 404;
                    await SendJsonAsync(context, new LocalDashboardErrorResponse { Error = "NotFound", Message = "Asset not found." }).ConfigureAwait(false);
                    return;
                }

                if (method == "GET" && String.Equals(path, "/health", StringComparison.OrdinalIgnoreCase))
                {
                    await SendJsonAsync(context, new LocalDashboardErrorResponse { Error = "Healthy", Message = "Xeno local dashboard is healthy." }).ConfigureAwait(false);
                    return;
                }

                if (path.StartsWith("/api/v1/", StringComparison.OrdinalIgnoreCase)) EnsureAuthorized(context);
                await HandleApiAsync(context, method, path, context.Token).ConfigureAwait(false);
            }
            catch (UnauthorizedAccessException ex)
            {
                context.Response.StatusCode = 401;
                await SendJsonAsync(context, new LocalDashboardErrorResponse { Error = "Unauthorized", Message = ex.Message }).ConfigureAwait(false);
            }
            catch (ArgumentException ex)
            {
                context.Response.StatusCode = 400;
                await SendJsonAsync(context, new LocalDashboardErrorResponse { Error = "BadRequest", Message = ex.Message }).ConfigureAwait(false);
            }
            catch (KeyNotFoundException ex)
            {
                context.Response.StatusCode = 404;
                await SendJsonAsync(context, new LocalDashboardErrorResponse { Error = "NotFound", Message = ex.Message }).ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                context.Response.StatusCode = 500;
                await SendJsonAsync(context, new LocalDashboardErrorResponse { Error = "InternalServerError", Message = ex.Message }).ConfigureAwait(false);
            }
        }

        private async Task HandleApiAsync(HttpContextBase context, string method, string path, CancellationToken token)
        {
            token.ThrowIfCancellationRequested();
            if (method == "GET" && String.Equals(path, "/api/v1/state", StringComparison.OrdinalIgnoreCase))
            {
                await SendJsonAsync(context, new LocalDashboardStateResponse
                {
                    Settings = RedactedSettings(),
                    Connection = ClientConnectionState.Load(),
                    DashboardUrl = DashboardUrl,
                    DashboardStatus = LocalDashboardStatusEnum.Listening
                }).ConfigureAwait(false);
                return;
            }

            if (method == "GET" && String.Equals(path, "/api/v1/statistics", StringComparison.OrdinalIgnoreCase))
            {
                await SendJsonAsync(context, await _History.StatisticsAsync(token).ConfigureAwait(false)).ConfigureAwait(false);
                return;
            }

            if (method == "GET" && String.Equals(path, "/api/v1/database", StringComparison.OrdinalIgnoreCase))
            {
                await SendJsonAsync(context, await BuildDatabaseResponseAsync(token).ConfigureAwait(false)).ConfigureAwait(false);
                return;
            }

            if (method == "GET" && String.Equals(path, "/api/v1/requests/analytics/overview", StringComparison.OrdinalIgnoreCase))
            {
                await SendJsonAsync(context, await _History.StatisticsAsync(token).ConfigureAwait(false)).ConfigureAwait(false);
                return;
            }

            if (method == "GET" && String.Equals(path, "/api/v1/telemetry/status", StringComparison.OrdinalIgnoreCase))
            {
                ClientTelemetryPersistenceStatus status = await _TelemetryHistory.GetStatusAsync(_Settings.Telemetry, token).ConfigureAwait(false);
                ApplyRuntimeTelemetryStatus(status);
                await SendJsonAsync(context, status).ConfigureAwait(false);
                return;
            }

            if (method == "GET" && String.Equals(path, "/api/v1/telemetry/latest", StringComparison.OrdinalIgnoreCase))
            {
                ClientTelemetrySampleDetail? detail = await _TelemetryHistory.ReadLatestAsync(token).ConfigureAwait(false);
                ClientTelemetryPersistenceStatus status = await _TelemetryHistory.GetStatusAsync(_Settings.Telemetry, token).ConfigureAwait(false);
                ApplyRuntimeTelemetryStatus(status);
                await SendJsonAsync(context, new ClientTelemetryLatestResponse { Snapshot = _TelemetryService?.LatestSnapshot, Detail = detail, Status = status }).ConfigureAwait(false);
                return;
            }

            if (method == "POST" && String.Equals(path, "/api/v1/telemetry/collect", StringComparison.OrdinalIgnoreCase))
            {
                ClientTelemetrySnapshot? snapshot = _TelemetryService == null ? null : await _TelemetryService.CollectNowAsync(token).ConfigureAwait(false);
                ClientTelemetrySampleDetail? detail = await _TelemetryHistory.ReadLatestAsync(token).ConfigureAwait(false);
                ClientTelemetryPersistenceStatus status = await _TelemetryHistory.GetStatusAsync(_Settings.Telemetry, token).ConfigureAwait(false);
                ApplyRuntimeTelemetryStatus(status);
                await SendJsonAsync(context, new ClientTelemetryLatestResponse { Snapshot = snapshot, Detail = detail, Status = status }).ConfigureAwait(false);
                return;
            }

            if (method == "POST" && String.Equals(path, "/api/v1/telemetry/history/search", StringComparison.OrdinalIgnoreCase))
            {
                ClientTelemetrySearchRequest request = ReadJson<ClientTelemetrySearchRequest>(context);
                ApplyTelemetryRangeDefaults(request, context);
                await SendJsonAsync(context, await _TelemetryHistory.SearchAsync(request, token).ConfigureAwait(false)).ConfigureAwait(false);
                return;
            }

            if (method == "POST" && String.Equals(path, "/api/v1/telemetry/history/rollups", StringComparison.OrdinalIgnoreCase))
            {
                ClientTelemetryRollupRequest request = ReadJson<ClientTelemetryRollupRequest>(context);
                ApplyTelemetryRangeDefaults(request, context);
                await SendJsonAsync(context, await _TelemetryHistory.RollupAsync(request, token).ConfigureAwait(false)).ConfigureAwait(false);
                return;
            }

            if (method == "POST" && String.Equals(path, "/api/v1/telemetry/history/delete", StringComparison.OrdinalIgnoreCase))
            {
                int removed = await _TelemetryHistory.DeleteAllAsync(token).ConfigureAwait(false);
                await SendJsonAsync(context, new LocalDashboardMutationResponse { Removed = removed, RemovedOne = removed > 0 }).ConfigureAwait(false);
                return;
            }

            if (method == "GET" && String.Equals(path, "/api/v1/logs", StringComparison.OrdinalIgnoreCase))
            {
                int tail = ParseInt(context.Request.Query.Elements.Get("tail"), 100);
                string severity = context.Request.Query.Elements.Get("severity") ?? "all";
                await SendJsonAsync(context, new LocalDashboardLogResponse { LogFile = ClientConnectionState.LogFile, Lines = ReadLogTail(ClientConnectionState.LogFile, tail, severity) }).ConfigureAwait(false);
                return;
            }

            if (method == "GET" && String.Equals(path, "/api/v1/providers", StringComparison.OrdinalIgnoreCase))
            {
                await SendJsonAsync(context, new LocalProviderListResponse { Providers = _Settings.Runners.Select(LocalSecretRedactor.RedactProvider).ToList() }).ConfigureAwait(false);
                return;
            }

            if (method == "POST" && String.Equals(path, "/api/v1/providers", StringComparison.OrdinalIgnoreCase))
            {
                LocalProviderUpsertRequest request = ReadJson<LocalProviderUpsertRequest>(context);
                ClientRunnerSettings provider = UpsertProvider(request, null);
                await SaveInventoryConfigurationAsync(token).ConfigureAwait(false);
                await SendJsonAsync(context, LocalSecretRedactor.RedactProvider(provider)).ConfigureAwait(false);
                return;
            }

            if ((method == "PUT" || method == "DELETE") && path.StartsWith("/api/v1/providers/", StringComparison.OrdinalIgnoreCase))
            {
                string name = DecodePathSegment(path.Substring("/api/v1/providers/".Length));
                if (method == "PUT" && name.EndsWith("/test", StringComparison.OrdinalIgnoreCase))
                {
                    name = name.Substring(0, name.Length - "/test".Length).TrimEnd('/');
                    await SendJsonAsync(context, await TestProviderAsync(name, token).ConfigureAwait(false)).ConfigureAwait(false);
                    return;
                }

                if (method == "PUT")
                {
                    LocalProviderUpsertRequest request = ReadJson<LocalProviderUpsertRequest>(context);
                    ClientRunnerSettings provider = UpsertProvider(request, name);
                    await SaveInventoryConfigurationAsync(token).ConfigureAwait(false);
                    await SendJsonAsync(context, LocalSecretRedactor.RedactProvider(provider)).ConfigureAwait(false);
                    return;
                }

                int removed = _Settings.Runners.RemoveAll(item => String.Equals(item.Name, name, StringComparison.OrdinalIgnoreCase));
                await SaveInventoryConfigurationAsync(token).ConfigureAwait(false);
                await SendJsonAsync(context, new LocalDashboardMutationResponse { Removed = removed, RemovedOne = removed > 0 }).ConfigureAwait(false);
                return;
            }

            if (method == "POST" && path.StartsWith("/api/v1/providers/", StringComparison.OrdinalIgnoreCase) && path.EndsWith("/test", StringComparison.OrdinalIgnoreCase))
            {
                string rawName = path.Substring("/api/v1/providers/".Length);
                rawName = rawName.Substring(0, rawName.Length - "/test".Length);
                string name = DecodePathSegment(rawName);
                await SendJsonAsync(context, await TestProviderAsync(name, token).ConfigureAwait(false)).ConfigureAwait(false);
                return;
            }

            if (method == "GET" && String.Equals(path, "/api/v1/models", StringComparison.OrdinalIgnoreCase))
            {
                await SendJsonAsync(context, BuildModelList()).ConfigureAwait(false);
                return;
            }

            if (method == "POST" && String.Equals(path, "/api/v1/models", StringComparison.OrdinalIgnoreCase))
            {
                LocalModelUpsertRequest request = ReadJson<LocalModelUpsertRequest>(context);
                LocalModelEntry model = UpsertModel(request, null, null);
                await SaveInventoryConfigurationAsync(token).ConfigureAwait(false);
                await SendJsonAsync(context, model).ConfigureAwait(false);
                return;
            }

            if ((method == "PUT" || method == "DELETE") && path.StartsWith("/api/v1/models/", StringComparison.OrdinalIgnoreCase))
            {
                string[] parts = DecodePathSegments(path.Substring("/api/v1/models/".Length), 2);
                if (parts.Length != 2) throw new ArgumentException("Model route must include provider and model name.");
                if (method == "PUT")
                {
                    LocalModelUpsertRequest request = ReadJson<LocalModelUpsertRequest>(context);
                    LocalModelEntry model = UpsertModel(request, parts[0], parts[1]);
                    await SaveInventoryConfigurationAsync(token).ConfigureAwait(false);
                    await SendJsonAsync(context, model).ConfigureAwait(false);
                    return;
                }

                int removed = DeleteModel(parts[0], parts[1]);
                await SaveInventoryConfigurationAsync(token).ConfigureAwait(false);
                await SendJsonAsync(context, new LocalDashboardMutationResponse { Removed = removed, RemovedOne = removed > 0 }).ConfigureAwait(false);
                return;
            }

            if (method == "GET" && String.Equals(path, "/api/v1/requests", StringComparison.OrdinalIgnoreCase))
            {
                int limit = ParseInt(context.Request.Query.Elements.Get("limit"), 100);
                await SendJsonAsync(context, new LocalRequestHistoryListResponse { Entries = (await _History.ListAsync(limit, token).ConfigureAwait(false)).ToList() }).ConfigureAwait(false);
                return;
            }

            if (method == "GET" && String.Equals(path, "/api/v1/requests/summary", StringComparison.OrdinalIgnoreCase))
            {
                DateTime toUtc = DateTime.UtcNow;
                string range = context.Request.Query.Elements.Get("range") ?? "hour";
                DateTime fromUtc = SummaryStartUtc(range, toUtc);
                int bucketMinutes = ParseInt(context.Request.Query.Elements.Get("bucketMinutes"), DefaultBucketMinutes(range));
                LocalRequestHistorySummaryResponse response = new LocalRequestHistorySummaryResponse
                {
                    Buckets = (await _History.SummarizeAsync(fromUtc, toUtc, bucketMinutes, token).ConfigureAwait(false)).ToList()
                };
                await SendJsonAsync(context, response).ConfigureAwait(false);
                return;
            }

            if (method == "POST" && String.Equals(path, "/api/v1/requests/bulk", StringComparison.OrdinalIgnoreCase))
            {
                int removed = await _History.DeleteAllAsync(token).ConfigureAwait(false);
                await SendJsonAsync(context, new LocalDashboardMutationResponse { Removed = removed, RemovedOne = removed > 0 }).ConfigureAwait(false);
                return;
            }

            if ((method == "GET" || method == "DELETE") && path.StartsWith("/api/v1/requests/", StringComparison.OrdinalIgnoreCase))
            {
                string id = DecodePathSegment(path.Substring("/api/v1/requests/".Length));
                if (method == "GET")
                {
                    LocalRequestHistoryEntry? entry = await _History.GetAsync(id, token).ConfigureAwait(false);
                    if (entry == null) throw new KeyNotFoundException("Request history entry not found.");
                    await SendJsonAsync(context, entry).ConfigureAwait(false);
                    return;
                }

                bool removed = await _History.DeleteAsync(id, token).ConfigureAwait(false);
                await SendJsonAsync(context, new LocalDashboardMutationResponse { Removed = removed ? 1 : 0, RemovedOne = removed }).ConfigureAwait(false);
                return;
            }

            if ((method == "GET" || method == "DELETE") && path.StartsWith("/api/v1/telemetry/history/", StringComparison.OrdinalIgnoreCase))
            {
                string id = DecodePathSegment(path.Substring("/api/v1/telemetry/history/".Length));
                if (method == "GET")
                {
                    ClientTelemetrySampleDetail? detail = await _TelemetryHistory.ReadDetailAsync(id, token).ConfigureAwait(false);
                    if (detail == null) throw new KeyNotFoundException("Telemetry sample not found.");
                    await SendJsonAsync(context, detail).ConfigureAwait(false);
                    return;
                }

                bool removed = await _TelemetryHistory.DeleteAsync(id, token).ConfigureAwait(false);
                await SendJsonAsync(context, new LocalDashboardMutationResponse { Removed = removed ? 1 : 0, RemovedOne = removed }).ConfigureAwait(false);
                return;
            }

            if (method == "GET" && String.Equals(path, "/api/v1/settings", StringComparison.OrdinalIgnoreCase))
            {
                await SendJsonAsync(context, new LocalDashboardSettingsResponse { Settings = RedactedSettings(), SettingsFile = ClientSettings.SettingsFile }).ConfigureAwait(false);
                return;
            }

            if (method == "PUT" && String.Equals(path, "/api/v1/settings", StringComparison.OrdinalIgnoreCase))
            {
                LocalClientSettingsUpdateRequest request = ReadJson<LocalClientSettingsUpdateRequest>(context);
                ValidateHost(request.Host);
                _Settings.LocalDashboard.Enabled = request.Enabled;
                _Settings.LocalDashboard.Host = request.Host;
                _Settings.LocalDashboard.Port = Math.Max(1, request.Port);
                _Settings.LocalDashboard.OpenBrowserOnStart = request.OpenBrowserOnStart;
                _Settings.LocalDashboard.RequireLocalToken = request.RequireLocalToken;
                _Settings.LocalDashboard.Database = request.Database ?? new ClientLocalDatabaseSettings();
                _Settings.LocalDashboard.HistoryRetention = request.HistoryRetention ?? new ClientLocalHistorySettings();
                _Settings.Telemetry = request.Telemetry ?? new ClientTelemetrySettings();
                _Settings.Save();
                await SendJsonAsync(context, new LocalDashboardSettingsResponse { Settings = RedactedSettings(), SettingsFile = ClientSettings.SettingsFile, RestartRequired = true }).ConfigureAwait(false);
                return;
            }

            context.Response.StatusCode = 404;
            await SendJsonAsync(context, new LocalDashboardErrorResponse { Error = "NotFound", Message = "Route not found." }).ConfigureAwait(false);
        }

        private ClientSettings RedactedSettings()
        {
            ClientSettings copy = new ClientSettings
            {
                ClientId = _Settings.ClientId,
                TenantId = _Settings.TenantId,
                Name = _Settings.Name,
                UseColors = _Settings.UseColors,
                Server = new ClientServerSettings
                {
                    Url = _Settings.Server.Url,
                    RestUrl = _Settings.Server.RestUrl,
                    AccessKey = _Settings.Server.AccessKey,
                    SecretKey = _Settings.Server.SecretKey,
                    Token = _Settings.Server.Token
                },
                LocalDashboard = _Settings.LocalDashboard,
                Telemetry = _Settings.Telemetry,
                Providers = _Settings.Runners.Select(LocalSecretRedactor.RedactProvider).ToList()
            };
            copy.Server.SecretKey = String.IsNullOrWhiteSpace(copy.Server.SecretKey) ? null : "configured";
            copy.Server.Token = String.IsNullOrWhiteSpace(copy.Server.Token) ? null : "configured";
            return copy;
        }

        private void ApplyRuntimeTelemetryStatus(ClientTelemetryPersistenceStatus status)
        {
            ClientTelemetryPersistenceStatus? runtime = _TelemetryService?.Status;
            if (runtime == null) return;
            status.Running = runtime.Running;
            status.LastCollectionUtc = runtime.LastCollectionUtc;
            status.LastPruneUtc = runtime.LastPruneUtc;
            status.LastMessage = runtime.LastMessage;
        }

        private static void ApplyTelemetryRangeDefaults(ClientTelemetrySearchRequest request, HttpContextBase context)
        {
            DateTime toUtc = request.ToUtc ?? DateTime.UtcNow;
            request.ToUtc = toUtc;
            request.FromUtc ??= SummaryStartUtc(context.Request.Query.Elements.Get("range") ?? "hour", toUtc);
            request.Limit = Math.Clamp(request.Limit, 1, 1000);
            request.Offset = Math.Max(0, request.Offset);
        }

        private static void ApplyTelemetryRangeDefaults(ClientTelemetryRollupRequest request, HttpContextBase context)
        {
            DateTime toUtc = request.ToUtc ?? DateTime.UtcNow;
            string range = context.Request.Query.Elements.Get("range") ?? "hour";
            request.ToUtc = toUtc;
            request.FromUtc ??= SummaryStartUtc(range, toUtc);
            if (request.BucketMinutes <= 0) request.BucketMinutes = DefaultBucketMinutes(range);
        }

        private async Task<LocalDashboardDatabaseResponse> BuildDatabaseResponseAsync(CancellationToken token)
        {
            string resolvedFilename = _Settings.LocalDashboard.Database.ResolveFilename();
            FileInfo file = new FileInfo(resolvedFilename);
            LocalDashboardStatisticsResponse statistics = await _History.StatisticsAsync(token).ConfigureAwait(false);
            return new LocalDashboardDatabaseResponse
            {
                Status = file.Exists ? LocalDashboardDatabaseStatusEnum.Ready : LocalDashboardDatabaseStatusEnum.Missing,
                Filename = _Settings.LocalDashboard.Database.Filename,
                ResolvedFilename = resolvedFilename,
                Exists = file.Exists,
                SizeBytes = file.Exists ? file.Length : 0,
                Requests = statistics.Requests,
                OldestRequestUtc = statistics.OldestRequestUtc,
                NewestRequestUtc = statistics.NewestRequestUtc
            };
        }

        private async Task SaveInventoryConfigurationAsync(CancellationToken token)
        {
            _Settings.Save();
            await LocalDashboardRuntime.SnapshotConfigurationAsync(_History, _Settings, token).ConfigureAwait(false);
            ClientConnectionState.MarkInventoryUpdateRequested();
        }

        private ClientRunnerSettings UpsertProvider(LocalProviderUpsertRequest request, string? routeName)
        {
            string name = String.IsNullOrWhiteSpace(routeName) ? request.Name : routeName;
            if (String.IsNullOrWhiteSpace(name)) throw new ArgumentException("Provider name is required.");
            if (String.IsNullOrWhiteSpace(request.Endpoint)) throw new ArgumentException("Provider endpoint is required.");
            ClientRunnerSettings? provider = _Settings.Runners.FirstOrDefault(item => String.Equals(item.Name, name, StringComparison.OrdinalIgnoreCase));
            if (provider == null)
            {
                provider = new ClientRunnerSettings { Name = name, Models = new List<ClientModelSettings>() };
                _Settings.Runners.Add(provider);
            }

            provider.ApiType = String.IsNullOrWhiteSpace(request.ApiType) ? provider.ApiType : request.ApiType;
            provider.Endpoint = request.Endpoint;
            provider.TimeoutMs = Math.Max(1000, request.TimeoutMs);
            if (request.ClearCredential) provider.Credential = null;
            else if (!String.IsNullOrWhiteSpace(request.Credential)) provider.Credential = request.Credential;
            return provider;
        }

        private static DateTime SummaryStartUtc(string range, DateTime toUtc)
        {
            if (String.Equals(range, "day", StringComparison.OrdinalIgnoreCase)) return toUtc.AddDays(-1);
            if (String.Equals(range, "week", StringComparison.OrdinalIgnoreCase)) return toUtc.AddDays(-7);
            if (String.Equals(range, "month", StringComparison.OrdinalIgnoreCase)) return toUtc.AddDays(-30);
            return toUtc.AddHours(-1);
        }

        private static int DefaultBucketMinutes(string range)
        {
            if (String.Equals(range, "day", StringComparison.OrdinalIgnoreCase)) return 15;
            if (String.Equals(range, "week", StringComparison.OrdinalIgnoreCase)) return 120;
            if (String.Equals(range, "month", StringComparison.OrdinalIgnoreCase)) return 480;
            return 1;
        }

        private async Task<LocalProviderTestResponse> TestProviderAsync(string name, CancellationToken token)
        {
            if (String.IsNullOrWhiteSpace(name)) throw new ArgumentException("Provider name is required.");
            ClientRunnerSettings provider = _Settings.Runners.FirstOrDefault(item => String.Equals(item.Name, name, StringComparison.OrdinalIgnoreCase))
                ?? throw new KeyNotFoundException("Provider not found.");
            LocalProviderTestResponse response = new LocalProviderTestResponse
            {
                Name = provider.Name,
                Endpoint = provider.Endpoint
            };

            try
            {
                using (HttpClient httpClient = new HttpClient())
                {
                    httpClient.Timeout = TimeSpan.FromSeconds(5);
                    using (HttpRequestMessage request = new HttpRequestMessage(System.Net.Http.HttpMethod.Get, provider.Endpoint))
                    using (HttpResponseMessage httpResponse = await httpClient.SendAsync(request, token).ConfigureAwait(false))
                    {
                        response.StatusCode = (int)httpResponse.StatusCode;
                        response.Success = (int)httpResponse.StatusCode >= 200 && (int)httpResponse.StatusCode < 500;
                    }
                }
            }
            catch (Exception ex) when (ex is HttpRequestException || ex is TaskCanceledException || ex is InvalidOperationException)
            {
                response.Success = false;
                response.Error = ex.Message;
            }

            return response;
        }

        private LocalModelEntry UpsertModel(LocalModelUpsertRequest request, string? routeProvider, string? routeModel)
        {
            string providerName = String.IsNullOrWhiteSpace(routeProvider) ? request.Provider : routeProvider;
            string modelName = String.IsNullOrWhiteSpace(routeModel) ? request.Name : routeModel;
            if (String.IsNullOrWhiteSpace(providerName)) throw new ArgumentException("Provider name is required.");
            if (String.IsNullOrWhiteSpace(modelName)) throw new ArgumentException("Model name is required.");
            ClientRunnerSettings provider = _Settings.Runners.FirstOrDefault(item => String.Equals(item.Name, providerName, StringComparison.OrdinalIgnoreCase))
                ?? throw new KeyNotFoundException("Provider not found.");
            ClientModelSettings? model = provider.Models.FirstOrDefault(item => String.Equals(item.Name, modelName, StringComparison.OrdinalIgnoreCase));
            if (model == null)
            {
                model = new ClientModelSettings { Name = modelName };
                provider.Models.Add(model);
            }

            model.SupportsCompletions = request.SupportsCompletions;
            model.SupportsEmbeddings = request.SupportsEmbeddings;
            return ToModelEntry(provider, model);
        }

        private int DeleteModel(string providerName, string modelName)
        {
            ClientRunnerSettings provider = _Settings.Runners.FirstOrDefault(item => String.Equals(item.Name, providerName, StringComparison.OrdinalIgnoreCase))
                ?? throw new KeyNotFoundException("Provider not found.");
            return provider.Models.RemoveAll(item => String.Equals(item.Name, modelName, StringComparison.OrdinalIgnoreCase));
        }

        private LocalModelListResponse BuildModelList()
        {
            LocalModelListResponse response = new LocalModelListResponse();
            foreach (ClientRunnerSettings provider in _Settings.Runners)
            {
                foreach (ClientModelSettings model in provider.Models)
                {
                    response.Models.Add(ToModelEntry(provider, model));
                }
            }

            return response;
        }

        private static LocalModelEntry ToModelEntry(ClientRunnerSettings provider, ClientModelSettings model)
        {
            return new LocalModelEntry
            {
                Provider = provider.Name,
                ApiType = provider.ApiType,
                Endpoint = provider.Endpoint,
                Name = model.Name,
                SupportsCompletions = model.SupportsCompletions,
                SupportsEmbeddings = model.SupportsEmbeddings
            };
        }

        private void EnsureAuthorized(HttpContextBase context)
        {
            if (!_Settings.LocalDashboard.RequireLocalToken) return;
            string header = context.Request.Headers.Get("X-Xeno-Local-Token") ?? string.Empty;
            if (!String.Equals(header, _Token, StringComparison.Ordinal)) throw new UnauthorizedAccessException("Local dashboard token is missing or invalid.");
        }

        private static bool IsRootAsset(string path)
        {
            return String.Equals(path, "/logo.png", StringComparison.OrdinalIgnoreCase)
                || String.Equals(path, "/logo.ico", StringComparison.OrdinalIgnoreCase)
                || String.Equals(path, "/favicon.ico", StringComparison.OrdinalIgnoreCase)
                || String.Equals(path, "/config.js", StringComparison.OrdinalIgnoreCase);
        }

        private static T ReadJson<T>(HttpContextBase context) where T : new()
        {
            string json = context.Request.DataAsString ?? "{}";
            T? value = JsonUtil.Deserialize<T>(json);
            return value == null ? new T() : value;
        }

        private static int ParseInt(string? value, int defaultValue)
        {
            if (Int32.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out int parsed)) return parsed;
            return defaultValue;
        }

        private static List<string> ReadLogTail(string filename, int tail, string severity)
        {
            List<string> lines = new List<string>();
            if (!File.Exists(filename) || tail <= 0) return lines;
            string filter = severity ?? "all";
            using (FileStream stream = new FileStream(filename, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete))
            using (StreamReader reader = new StreamReader(stream))
            {
                while (!reader.EndOfStream)
                {
                    string? line = reader.ReadLine();
                    if (line == null) break;
                    if (!String.Equals(filter, "all", StringComparison.OrdinalIgnoreCase) && line.IndexOf("[" + filter.ToUpperInvariant() + "]", StringComparison.OrdinalIgnoreCase) < 0) continue;
                    lines.Add(line);
                    if (lines.Count > tail) lines.RemoveAt(0);
                }
            }

            return lines;
        }

        private static string DecodePathSegment(string value)
        {
            return Uri.UnescapeDataString(value ?? string.Empty).Trim('/');
        }

        private static string[] DecodePathSegments(string value, int expected)
        {
            string raw = (value ?? string.Empty).Trim('/');
            string[] parts = raw.Split('/', expected, StringSplitOptions.None);
            for (int i = 0; i < parts.Length; i++)
            {
                parts[i] = Uri.UnescapeDataString(parts[i]);
            }

            return parts;
        }

        private static void ApplyHeaders(HttpContextBase context)
        {
            context.Response.Headers.Add("Cache-Control", "no-store");
            context.Response.Headers.Add("X-Content-Type-Options", "nosniff");
        }

        private static void ApplyCorsHeaders(HttpContextBase context)
        {
            string? origin = context.Request.Headers.Get("Origin");
            if (String.IsNullOrWhiteSpace(origin)) return;
            string? host = context.Request.Headers.Get("Host");
            if (String.IsNullOrWhiteSpace(host)) return;
            string expected = "http://" + host;
            if (!String.Equals(origin.TrimEnd('/'), expected, StringComparison.OrdinalIgnoreCase)) return;
            context.Response.Headers.Add("Access-Control-Allow-Origin", origin);
            context.Response.Headers.Add("Access-Control-Allow-Headers", "Content-Type, X-Xeno-Local-Token, Accept-Language");
            context.Response.Headers.Add("Access-Control-Allow-Methods", "GET, POST, PUT, DELETE, OPTIONS");
        }

        private static async Task SendJsonAsync(HttpContextBase context, object value)
        {
            context.Response.ContentType = "application/json";
            await context.Response.Send(JsonUtil.Serialize(value, true), context.Token).ConfigureAwait(false);
        }

        private static async Task SendHtmlAsync(HttpContextBase context, string html)
        {
            context.Response.ContentType = "text/html";
            await context.Response.Send(html, context.Token).ConfigureAwait(false);
        }

        private static async Task SendAssetAsync(HttpContextBase context, LocalDashboardAsset asset)
        {
            context.Response.ContentType = asset.ContentType;
            context.Response.Headers.Add("Cache-Control", asset.CacheControl);
            await context.Response.Send(asset.Bytes, context.Token).ConfigureAwait(false);
        }

        private string DashboardHtml()
        {
            string tokenScript = "<script>window.__XENO_LOCAL_TOKEN__='" + WebUtility.HtmlEncode(_Token) + "';</script>";
            return """
<!doctype html>
<html lang="en">
<head>
  <meta charset="utf-8" />
  <meta name="viewport" content="width=device-width, initial-scale=1" />
  <title>Xeno Local Dashboard</title>
  <style>
    :root{--color-bg:#f4f6f8;--color-panel:#fff;--color-panel-soft:#f8fafc;--color-text:#172026;--color-muted:#657282;--color-border:#dce3ea;--color-border-strong:#c5d0da;--color-table-row-hover:#fbfcfd;--color-accent:#16615d;--color-accent-strong:#0d4744;--color-danger:#c73737;--sidebar-bg:#1f262d;--sidebar-text:#c7d0d8;--sidebar-active:#fff;--shadow-soft:0 16px 40px rgba(25,32,38,.08);--radius:8px;font-family:Inter,ui-sans-serif,system-ui,-apple-system,BlinkMacSystemFont,"Segoe UI",Arial,sans-serif}html[data-theme=dark]{--color-bg:#12171c;--color-panel:#1e252c;--color-panel-soft:#252d35;--color-text:#edf3f7;--color-muted:#a7b2bd;--color-border:#34414c;--color-border-strong:#465764;--color-table-row-hover:#26313a;--shadow-soft:0 16px 40px rgba(0,0,0,.24)}*{box-sizing:border-box}body{margin:0;background:var(--color-bg);color:var(--color-text)}button,input,select,textarea{font:inherit}button{cursor:pointer}button:disabled{cursor:not-allowed;opacity:.62}.app{display:grid;grid-template-columns:242px 1fr;min-height:100vh}.side{background:var(--sidebar-bg);color:var(--sidebar-text);padding:18px}.brand{font-weight:800;font-size:18px;margin:2px 0 22px;color:var(--sidebar-active)}.nav{display:grid;gap:6px}.nav button{border:0;background:transparent;color:var(--sidebar-text);text-align:left;padding:10px 12px;border-radius:8px;font-weight:700}.nav button.active,.nav button:hover{background:rgba(255,255,255,.09);color:var(--sidebar-active)}.main{padding:18px;min-width:0}.top{display:flex;justify-content:space-between;align-items:center;margin-bottom:18px;gap:12px}.top strong{font-size:18px}.muted{color:var(--color-muted);font-size:13px}.actions,.toolbar,.modal-actions,.form-actions{display:flex;gap:8px;align-items:center;flex-wrap:wrap}.toolbar{justify-content:space-between;margin-bottom:10px}.icon,.action{border:1px solid var(--color-border);background:var(--color-panel);color:var(--color-text);border-radius:8px;min-height:34px;padding:7px 10px;text-decoration:none}.primary{background:var(--color-accent);border-color:var(--color-accent);color:#fff}.danger{border-color:rgba(199,55,55,.45);color:var(--color-danger)}.grid{display:grid;grid-template-columns:repeat(4,minmax(0,1fr));gap:12px;margin-bottom:14px}.card{background:var(--color-panel);border:1px solid var(--color-border);border-radius:var(--radius);box-shadow:var(--shadow-soft);padding:14px;margin-bottom:14px}.card h2{font-size:16px;margin:0 0 12px}.kpi span{display:block;color:var(--color-muted);font-size:12px;font-weight:800;text-transform:uppercase}.kpi strong{font-size:24px}.table-scroll{overflow:auto;border:1px solid var(--color-border);border-radius:8px}table{width:100%;border-collapse:collapse;background:var(--color-panel)}th,td{border-bottom:1px solid var(--color-border);padding:10px;text-align:left;font-size:13px;height:46px;vertical-align:middle}tr:hover td{background:var(--color-table-row-hover)}th{font-size:11px;text-transform:uppercase;color:var(--color-muted);background:var(--color-panel-soft);white-space:nowrap}td.actions-cell{width:1%;white-space:nowrap}.copy-wrap{display:inline-flex;align-items:center;gap:6px}.copy{border:1px solid var(--color-border);background:var(--color-panel-soft);color:var(--color-text);border-radius:6px;min-height:26px;padding:2px 7px}code,pre,textarea.mono{font-family:ui-monospace,SFMono-Regular,Consolas,"Liberation Mono",monospace}pre{white-space:pre-wrap;background:var(--color-panel-soft);border:1px solid var(--color-border);border-radius:8px;padding:12px;overflow:auto}.chart{height:250px;display:flex;align-items:end;gap:3px;border-left:1px solid var(--color-border);border-bottom:1px solid var(--color-border);padding:8px}.bar{flex:1;background:var(--color-accent);min-height:2px;border-radius:3px 3px 0 0}.modal{position:fixed;inset:3%;background:var(--color-panel);border:1px solid var(--color-border);border-radius:8px;box-shadow:0 20px 70px #0005;padding:18px;overflow:auto;z-index:10}.modal-head{display:flex;justify-content:space-between;align-items:flex-start;gap:12px;margin-bottom:14px}.modal-head h2{margin:0}.hidden{display:none}.form-grid{display:grid;grid-template-columns:repeat(2,minmax(0,1fr));gap:12px}.field{display:grid;gap:6px;color:var(--color-muted);font-size:12px;font-weight:700;text-transform:uppercase}.field input,.field select,.field textarea{min-height:38px;border:1px solid var(--color-border);border-radius:6px;background:var(--color-panel);color:var(--color-text);padding:8px 10px}.field textarea{min-height:110px;resize:vertical}.check{display:flex;align-items:center;gap:8px;text-transform:none;color:var(--color-text);font-size:13px}.full{grid-column:1/-1}.notice{background:var(--color-panel-soft);border:1px solid var(--color-border);border-radius:8px;padding:10px;color:var(--color-muted)}details{border:1px solid var(--color-border);border-radius:8px;margin-bottom:10px;background:var(--color-panel)}summary{cursor:pointer;padding:10px;font-weight:800}details .details-body{padding:0 10px 10px}.toast{position:fixed;right:18px;bottom:18px;background:var(--color-accent);color:#fff;padding:10px 12px;border-radius:8px;box-shadow:var(--shadow-soft);z-index:20}@media(max-width:900px){.app{grid-template-columns:1fr}.grid,.form-grid{grid-template-columns:1fr 1fr}}@media(max-width:650px){.grid,.form-grid{grid-template-columns:1fr}.top{align-items:flex-start;flex-direction:column}}
  </style>
</head>
<body>
""" + tokenScript + """
<div class="app" id="app">
  <aside class="side"><div class="brand">Xeno Local</div><div class="nav"><button class="active" data-page="home" title="Show connection state, request activity, and recent requests processed by this client.">Home</button><button data-page="providers" title="Create, edit, test, and delete local provider endpoints used by this client.">Providers</button><button data-page="models" title="Create, edit, and delete models advertised by this client to the Xeno server.">Models</button><button data-page="requests" title="Inspect persisted local request history captured by this client.">Requests</button><button data-page="logs" title="Read timestamped Xeno client logs from this machine.">Logs</button><button data-page="settings" title="Edit local dashboard settings and see which changes require restart.">Settings</button></div></aside>
  <main class="main"><div class="top"><div><strong id="title">Home</strong><div id="url" class="muted"></div></div><div class="actions"><a class="icon" href="https://github.com/jchristn/Xeno" rel="noreferrer" target="_blank" title="Open the Xeno GitHub repository.">GitHub</a><button class="icon" id="theme" title="Toggle the local dashboard between light and dark mode.">Theme</button><button class="icon" id="refresh" title="Refresh the current dashboard data from the local client service.">Refresh</button></div></div><div id="content"></div></main>
</div>
<div id="modal" class="modal hidden"></div>
<script>
const token=window.__XENO_LOCAL_TOKEN__;let page='home';let logTail=200;let logSeverity='all';let requestRange='hour';let modelProviderFilter='';let cache={providers:[],models:[],requests:[]};
async function api(p,o){o=o||{};o.headers=Object.assign({'X-Xeno-Local-Token':token,'Content-Type':'application/json','Accept-Language':navigator.language||'en'},o.headers||{});const r=await fetch(p,o);const text=await r.text();if(!r.ok)throw new Error(text||r.statusText);return text?JSON.parse(text):{};}
function esc(v){return String(v??'').replace(/[&<>"]/g,c=>({'&':'&amp;','<':'&lt;','>':'&gt;','"':'&quot;'}[c]));}
function titleCase(v){return String(v).replace(/([A-Z])/g,' $1').replace(/^./,c=>c.toUpperCase()).trim();}
function copyButton(value,label){return '<button class="copy" onclick="copyText(event,'+JSON.stringify(String(value??''))+')" title="'+esc(label||'Copy this value to the clipboard.')+'" type="button">Copy</button>'}
async function copyText(event,value){event.stopPropagation();try{await navigator.clipboard.writeText(value);}catch(e){const t=document.createElement('textarea');t.value=value;document.body.appendChild(t);t.select();document.execCommand('copy');t.remove();}event.target.textContent='Copied';setTimeout(()=>event.target.textContent='Copy',1100);}
function table(rows,cols,actions){const body=rows.length?rows.map((r,i)=>'<tr>'+cols.map(c=>cell(r,c)).join('')+(actions?'<td class="actions-cell">'+actions(r,i)+'</td>':'')+'</tr>').join(''):'<tr><td colspan="'+(cols.length+(actions?1:0))+'" class="muted">No records found.</td></tr>';return '<div class="table-scroll"><table><thead><tr>'+cols.map(c=>'<th>'+esc(c.label)+'</th>').join('')+(actions?'<th>Actions</th>':'')+'</tr></thead><tbody>'+body+'</tbody></table></div>'}
function cell(row,col){const v=row[col.key];let value=typeof v==='boolean'?(v?'yes':'no'):v;if(col.code&&value){return '<td><span class="copy-wrap"><code>'+esc(value)+'</code>'+copyButton(value,'Copy '+col.label+'.')+'</span></td>';}return '<td>'+esc(value??'-')+'</td>'}
async function load(){try{document.querySelectorAll('.nav button').forEach(b=>b.classList.toggle('active',b.dataset.page===page));document.getElementById('title').textContent=titleCase(page);if(page==='home')return home();if(page==='providers')return providers();if(page==='models')return models();if(page==='requests')return requests();if(page==='logs')return logs();return settings();}catch(e){document.getElementById('content').innerHTML='<div class="card"><h2>Unable To Load</h2><pre><code>'+esc(e.message)+'</code></pre></div>';}}
async function home(){const s=await api('/api/v1/state');const st=await api('/api/v1/statistics');const sum=await api('/api/v1/requests/summary?range='+encodeURIComponent(requestRange));const warn=s.settings?.localDashboard?.host==='0.0.0.0'?'<div class="notice"><strong>All-Interface Bind</strong><br>This dashboard is bound to 0.0.0.0 and may be reachable from other machines depending on firewall and network policy.</div>':'';document.getElementById('url').innerHTML=esc(s.dashboardUrl)+' '+copyButton(s.dashboardUrl,'Copy the local dashboard URL.');document.getElementById('content').innerHTML=warn+'<div class="grid">'+kpi('Connected',s.connection.isConnected)+' '+kpi('Registered',s.connection.isRegistered)+' '+kpi('Requests',st.requests)+' '+kpi('Failures',st.failures)+'</div><div class="card"><div class="toolbar"><h2>Requests Over Time</h2>'+rangeSelector()+'</div>'+chart(sum.buckets||[])+'</div><div class="card"><h2>Connection</h2>'+table([s.connection], [{key:'clientId',label:'Client ID',code:true},{key:'tenantId',label:'Tenant',code:true},{key:'webSocketUrl',label:'WebSocket'},{key:'lastDisconnectReason',label:'Last Reason'}])+'</div>'}
function kpi(l,v){return '<div class="card kpi"><span>'+esc(l)+'</span><strong>'+esc(v)+'</strong></div>'}
function chart(b){if(!b.length)return '<div class="notice">No requests have been recorded for this range.</div>';const max=Math.max(...b.map(x=>x.total||0),1);return '<div class="chart">'+b.map(x=>'<div class="bar" title="'+esc(x.startUtc)+' total '+esc(x.total)+' failed '+esc(x.failed)+'" style="height:'+Math.max(2,Math.round(((x.total||0)/max)*220))+'px"></div>').join('')+'</div>'}
async function providers(){const d=await api('/api/v1/providers');cache.providers=d.providers||[];document.getElementById('content').innerHTML='<div class="card"><div class="toolbar"><h2>Provider Endpoints</h2><button class="action primary" onclick="editProvider()" title="Create a new local provider endpoint.">Create Provider</button></div>'+table(cache.providers,[{key:'name',label:'Name',code:true},{key:'apiType',label:'API Type'},{key:'endpoint',label:'Endpoint'},{key:'timeoutMs',label:'Timeout ms'},{key:'credential',label:'Credential'}],(r,i)=>'<button class="action" onclick="testProvider('+i+')" title="Send a lightweight connectivity check to this provider endpoint.">Test</button> <button class="action" onclick="editProvider('+i+')" title="Edit this provider endpoint.">Edit</button> <button class="action danger" onclick="deleteProvider('+i+')" title="Delete this provider endpoint and its advertised models.">Delete</button>')+'</div>'}
async function models(){const d=await api('/api/v1/models');cache.models=d.models||[];const providerNames=[...new Set(cache.models.map(m=>m.provider).filter(Boolean))];const filtered=modelProviderFilter?cache.models.filter(m=>m.provider===modelProviderFilter):cache.models;const filter='<label class="field" title="Filter advertised models by provider endpoint.">Provider<select onchange="modelProviderFilter=this.value;models()"><option value="">All</option>'+providerNames.map(p=>'<option value="'+esc(p)+'" '+(p===modelProviderFilter?'selected':'')+'>'+esc(p)+'</option>').join('')+'</select></label>';document.getElementById('content').innerHTML='<div class="card"><div class="toolbar"><h2>Advertised Models</h2><div class="actions">'+filter+'<button class="action primary" onclick="editModel()" title="Create a model advertisement for one local provider.">Create Model</button></div></div>'+table(filtered,[{key:'name',label:'Model',code:true},{key:'provider',label:'Provider',code:true},{key:'apiType',label:'API Type'},{key:'supportsCompletions',label:'Completions'},{key:'supportsEmbeddings',label:'Embeddings'}],(r)=>'<button class="action" onclick="editModelByName('+JSON.stringify(r.provider)+','+JSON.stringify(r.name)+')" title="Edit advertised model capabilities.">Edit</button> <button class="action danger" onclick="deleteModelByName('+JSON.stringify(r.provider)+','+JSON.stringify(r.name)+')" title="Delete this model advertisement.">Delete</button>')+'</div>'}
async function requests(){const d=await api('/api/v1/requests');const sum=await api('/api/v1/requests/summary?range='+encodeURIComponent(requestRange));cache.requests=d.entries||[];document.getElementById('content').innerHTML='<div class="card"><div class="toolbar"><h2>Requests Over Time</h2>'+rangeSelector()+'</div>'+chart(sum.buckets||[])+'</div><div class="card"><div class="toolbar"><h2>Request History</h2><div class="actions"><button class="action" onclick="load()" title="Refresh local request history.">Refresh</button><button class="action danger" onclick="clearRequests()" title="Clear all local request history after confirmation.">Clear</button></div></div>'+table(cache.requests,[{key:'id',label:'ID',code:true},{key:'receivedUtc',label:'Received UTC'},{key:'method',label:'Method'},{key:'path',label:'Path'},{key:'modelName',label:'Model'},{key:'providerName',label:'Provider'},{key:'statusCode',label:'Status'},{key:'outcome',label:'Outcome'}],(r,i)=>'<button class="action" onclick="showRequest('+i+')" title="Open full request details.">Details</button> <button class="action danger" onclick="deleteRequest('+i+')" title="Delete this local request history entry.">Delete</button>')+'</div>'}
function rangeSelector(){return '<label class="field" title="Choose the time range used by request activity charts.">Range<select onchange="requestRange=this.value;load()"><option value="hour" '+selText('hour',requestRange)+'>Last Hour</option><option value="day" '+selText('day',requestRange)+'>Last Day</option><option value="week" '+selText('week',requestRange)+'>Last Week</option><option value="month" '+selText('month',requestRange)+'>Last Month</option></select></label>'}
function selText(value,current){return value===current?'selected':''}
async function logs(){const d=await api('/api/v1/logs?tail='+encodeURIComponent(logTail)+'&severity='+encodeURIComponent(logSeverity));document.getElementById('content').innerHTML='<div class="card"><div class="toolbar"><h2>Client Logs</h2><div class="actions"><label class="field" title="Choose which severity level to show from the client log file.">Severity<select onchange="logSeverity=this.value;logs()"><option value="all" '+selText('all',logSeverity)+'>All</option><option value="debug" '+selText('debug',logSeverity)+'>Debug</option><option value="info" '+selText('info',logSeverity)+'>Info</option><option value="warn" '+selText('warn',logSeverity)+'>Warn</option><option value="error" '+selText('error',logSeverity)+'>Error</option></select></label><label class="field" title="Choose how many recent log lines to read from the client log file.">Tail<select id="tail" onchange="logTail=Number(this.value);logs()"><option '+sel(50,logTail)+'>50</option><option '+sel(100,logTail)+'>100</option><option '+sel(200,logTail)+'>200</option><option '+sel(500,logTail)+'>500</option></select></label><button class="action" onclick="load()" title="Read the latest log lines.">Refresh</button></div></div><pre><code>'+esc((d.lines||[]).join('\n'))+'</code></pre></div>'}
function sel(value,current){return value===current?'selected':''}
async function settings(){const d=await api('/api/v1/settings');const db=await api('/api/v1/database');const s=d.settings.localDashboard;document.getElementById('content').innerHTML='<div class="card"><div class="toolbar"><h2>Local Dashboard Settings</h2>'+copyButton(JSON.stringify(d,null,2),'Copy the current settings JSON.')+'</div><div class="notice">Host, port, token, and database file changes are saved to client.json but take effect after the client restarts.</div><div class="form-grid">'+checkField('enabled','Enabled',s.enabled,'Allow the Xeno client to start the local dashboard service when the worker starts.')+field('host','Host',s.host,'Bind host. Defaults to 127.0.0.1; 0.0.0.0 intentionally listens on all interfaces.')+field('port','Port',s.port,'HTTP port used by the local dashboard.', 'number')+checkField('requireLocalToken','Require Local Token',s.requireLocalToken,'Require the per-process token injected into the dashboard page for local API calls.')+checkField('openBrowserOnStart','Open Browser On Start',s.openBrowserOnStart,'Open the local dashboard automatically after it starts.')+field('filename','SQLite Filename',s.database?.filename||'','Local SQLite database file used for request history.')+field('maxEntries','Max History Entries',s.historyRetention?.maxEntries||1000,'Maximum local request history rows retained.', 'number')+field('maxBodyBytes','Max Body Bytes',s.historyRetention?.maxBodyBytes||65536,'Maximum bytes retained from request and response bodies.', 'number')+checkField('persistBodies','Persist Bodies',s.historyRetention?.persistBodies,'Persist bounded request and response bodies for local inspection.')+'</div><div class="form-actions"><button class="action primary" onclick="saveSettings()" title="Save local dashboard settings to client.json.">Save Settings</button></div></div><div class="card"><div class="toolbar"><h2>Database Status</h2>'+copyButton(JSON.stringify(db,null,2),'Copy local database status JSON.')+'</div>'+table([db],[{key:'status',label:'Status'},{key:'resolvedFilename',label:'Resolved Filename',code:true},{key:'sizeBytes',label:'Size Bytes'},{key:'requests',label:'Requests'},{key:'oldestRequestUtc',label:'Oldest UTC'},{key:'newestRequestUtc',label:'Newest UTC'}])+'</div>'}
function field(id,label,value,tip,type){return '<label class="field" title="'+esc(tip)+'">'+esc(label)+'<input id="'+id+'" type="'+esc(type||'text')+'" value="'+esc(value??'')+'"></label>'}
function checkField(id,label,checked,tip){return '<label class="field check" title="'+esc(tip)+'"><input id="'+id+'" type="checkbox" '+(checked?'checked':'')+'> '+esc(label)+'</label>'}
function modal(title,body,actions){document.getElementById('modal').classList.remove('hidden');document.getElementById('modal').innerHTML='<div class="modal-head"><div><h2>'+esc(title)+'</h2></div><div class="actions">'+(actions||'')+'<button class="icon" onclick="closeModal()" title="Close this dialog.">Close</button></div></div>'+body}
function closeModal(){document.getElementById('modal').classList.add('hidden');document.getElementById('modal').innerHTML=''}
function editProvider(index){const p=index===undefined?{}:cache.providers[index];modal(index===undefined?'Create Provider':'Edit Provider','<div class="form-grid">'+field('providerName','Name',p.name||'','Friendly provider endpoint name used in xeno model commands.')+field('providerApiType','API Type',p.apiType||'Ollama','Provider API type such as Ollama, OpenAI, Gemini, or compatible endpoint.')+field('providerEndpoint','Endpoint',p.endpoint||'','Base URL for the provider endpoint.')+field('providerTimeout','Timeout ms',p.timeoutMs||300000,'Upstream request timeout in milliseconds.','number')+field('providerCredential','Credential','','Replacement credential. Leave blank to keep the current credential.')+checkField('providerClearCredential','Clear Credential',false,'Remove the stored credential for this provider.')+'</div>','<button class="action primary" onclick="saveProvider('+JSON.stringify(p.name||'')+')" title="Save this provider endpoint.">Save</button>')}
async function saveProvider(original){const body={name:val('providerName'),apiType:val('providerApiType'),endpoint:val('providerEndpoint'),timeoutMs:Number(val('providerTimeout')||300000),credential:val('providerCredential')||null,clearCredential:checked('providerClearCredential')};await api(original?'/api/v1/providers/'+encodeURIComponent(original):'/api/v1/providers',{method:original?'PUT':'POST',body:JSON.stringify(body)});closeModal();toast('Provider saved.');providers();}
async function testProvider(index){const p=cache.providers[index];const result=await api('/api/v1/providers/'+encodeURIComponent(p.name)+'/test',{method:'POST',body:'{}'});toast(result.success?'Provider is reachable.':'Provider test failed: '+(result.error||result.statusCode||'unknown'));}
async function deleteProvider(index){const p=cache.providers[index];if(!confirm('Delete provider '+p.name+'?'))return;await api('/api/v1/providers/'+encodeURIComponent(p.name),{method:'DELETE'});toast('Provider deleted.');providers();}
async function editModel(index){const m=index===undefined?{}:cache.models[index];const providers=(await api('/api/v1/providers')).providers||[];const options=providers.map(p=>'<option value="'+esc(p.name)+'" '+(p.name===m.provider?'selected':'')+'>'+esc(p.name)+'</option>').join('');modal(index===undefined?'Create Model':'Edit Model','<div class="form-grid"><label class="field" title="Provider that owns this model advertisement.">Provider<select id="modelProvider" '+(index===undefined?'':'disabled')+'>'+options+'</select></label>'+field('modelName','Model',m.name||'','Model name advertised to the Xeno server.')+checkField('modelCompletions','Supports Completions',index===undefined?true:m.supportsCompletions,'Route chat, completion, or generation requests to this model.')+checkField('modelEmbeddings','Supports Embeddings',index===undefined?false:m.supportsEmbeddings,'Route embedding requests to this model.')+'</div>','<button class="action primary" onclick="saveModel('+JSON.stringify(m.provider||'')+','+JSON.stringify(m.name||'')+')" title="Save this model advertisement.">Save</button>')}
async function saveModel(routeProvider,routeName){const body={provider:val('modelProvider'),name:val('modelName'),supportsCompletions:checked('modelCompletions'),supportsEmbeddings:checked('modelEmbeddings')};const route=routeProvider?'/api/v1/models/'+encodeURIComponent(routeProvider)+'/'+encodeURIComponent(routeName):'/api/v1/models';await api(route,{method:routeProvider?'PUT':'POST',body:JSON.stringify(body)});closeModal();toast('Model saved.');models();}
async function deleteModel(index){const m=cache.models[index];if(!confirm('Delete model '+m.name+' from '+m.provider+'?'))return;await api('/api/v1/models/'+encodeURIComponent(m.provider)+'/'+encodeURIComponent(m.name),{method:'DELETE'});toast('Model deleted.');models();}
function editModelByName(provider,name){const index=cache.models.findIndex(m=>m.provider===provider&&m.name===name);editModel(index);}
function deleteModelByName(provider,name){const index=cache.models.findIndex(m=>m.provider===provider&&m.name===name);deleteModel(index);}
async function showRequest(index){const row=cache.requests[index];const d=await api('/api/v1/requests/'+encodeURIComponent(row.id));modal('Request Details',requestDetails(d),copyButton(JSON.stringify(d,null,2),'Copy full request JSON.'))}
function requestDetails(d){return '<div class="grid">'+kpi('Status',d.statusCode||'-')+kpi('Outcome',d.outcome)+kpi('Duration ms',d.telemetry?.totalDurationMs||'-')+kpi('Chunks',d.telemetry?.streamChunkCount||0)+'</div>'+table([{id:d.id,tenantId:d.tenantId,clientId:d.clientId,requestorIp:d.requestorIp,providerName:d.providerName,apiType:d.apiType,modelName:d.modelName}], [{key:'id',label:'ID',code:true},{key:'tenantId',label:'Tenant',code:true},{key:'clientId',label:'Client',code:true},{key:'requestorIp',label:'Requestor IP'},{key:'providerName',label:'Provider'},{key:'apiType',label:'API Type'},{key:'modelName',label:'Model'}])+section('Request Headers',JSON.stringify(d.requestHeaders||{},null,2))+section('Request Body',d.requestBody||'')+section('Response Headers',JSON.stringify(d.responseHeaders||{},null,2))+section('Response Body',d.responseBody||'')+section('Telemetry',JSON.stringify(d.telemetry||{},null,2))+section('Timeline',JSON.stringify(d.timeline||[],null,2))}
function section(title,text){return '<details open><summary>'+esc(title)+' '+copyButton(text,'Copy '+title+'.')+'</summary><div class="details-body"><pre><code>'+esc(text||'')+'</code></pre></div></details>'}
async function deleteRequest(index){const r=cache.requests[index];if(!confirm('Delete request '+r.id+'?'))return;await api('/api/v1/requests/'+encodeURIComponent(r.id),{method:'DELETE'});toast('Request deleted.');requests();}
async function clearRequests(){if(!confirm('Clear all local request history?'))return;await api('/api/v1/requests/bulk',{method:'POST',body:'{}'});toast('Request history cleared.');requests();}
async function saveSettings(){const body={enabled:checked('enabled'),host:val('host'),port:Number(val('port')||9343),openBrowserOnStart:checked('openBrowserOnStart'),requireLocalToken:checked('requireLocalToken'),database:{filename:val('filename')},historyRetention:{maxEntries:Number(val('maxEntries')||1000),maxBodyBytes:Number(val('maxBodyBytes')||65536),persistBodies:checked('persistBodies')}};await api('/api/v1/settings',{method:'PUT',body:JSON.stringify(body)});toast('Settings saved. Restart the client for listener and database changes.');settings();}
function val(id){return document.getElementById(id)?.value||''}
function checked(id){return !!document.getElementById(id)?.checked}
function toast(message){const el=document.createElement('div');el.className='toast';el.textContent=message;document.body.appendChild(el);setTimeout(()=>el.remove(),1800)}
document.querySelectorAll('.nav button').forEach(b=>b.onclick=()=>{page=b.dataset.page;load();});document.getElementById('refresh').onclick=load;document.getElementById('theme').onclick=()=>{const next=document.documentElement.getAttribute('data-theme')==='dark'?'light':'dark';document.documentElement.setAttribute('data-theme',next);};load();setInterval(load,15000);
</script>
</body>
</html>
""";
        }

        private void Dispose(bool disposing)
        {
            if (_Disposed) return;
            if (disposing)
            {
                Stop();
                IDisposable? disposable = _Webserver as IDisposable;
                disposable?.Dispose();
            }

            _Disposed = true;
        }
    }
}
