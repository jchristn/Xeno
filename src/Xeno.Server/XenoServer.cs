namespace Xeno.Server
{
    using System;
    using System.Collections.Concurrent;
    using System.Collections.Generic;
    using System.Globalization;
    using System.IO;
    using System.Linq;
    using System.Net;
    using System.Net.Mail;
    using System.Net.WebSockets;
    using System.Security.Cryptography;
    using System.Text;
    using System.Text.Json;
    using System.Threading;
    using System.Threading.Tasks;
    using WatsonWebserver;
    using WatsonWebserver.Core;
    using WatsonWebserver.Core.OpenApi;
    using WatsonWebserver.Core.WebSockets;
    using Xeno.Core;
    using Xeno.Core.Database;
    using Xeno.Core.Enums;
    using Xeno.Core.Exceptions;
    using Xeno.Core.Helpers;
    using Xeno.Core.Models;
    using Xeno.Core.Models.Telemetry;
    using Xeno.Core.Serialization;
    using Xeno.Core.Services;
    using Xeno.Core.Settings;
    using Xeno.Email;
    using Xeno.Email.Providers;
    using Xeno.Email.Templates;
    using Xeno.Email.Workflows;
    using Xeno.Server.Services;

    /// <summary>
    /// Watson-backed Xeno REST, OpenAPI, Swagger, and WebSocket server host.
    /// </summary>
    public sealed class XenoServer : IDisposable
    {
        private const string RoutingTraceExposeHeaders = "X-Client-ID, X-Xeno-Request-ID, X-Xeno-Tenant-ID, X-Xeno-Model, X-Xeno-Routing-Outcome, X-Xeno-Routing-Message, X-Xeno-Routing-Policy-ID, X-Xeno-Routing-Policy-Name, X-Xeno-Routing-Policy-Match, X-Xeno-Routing-Policy-Reason, X-Xeno-Routing-Candidate-Count, X-Xeno-Routing-Candidates, X-Xeno-Load-Balancing-Mode, X-Xeno-Adaptive-Sample-Count, X-Xeno-Adaptive-Score, X-Xeno-Selection-Reason, X-Xeno-Selected-Client-Name, X-Xeno-Selected-API-Type, X-Xeno-Selected-Provider, X-Xeno-Selected-Source, X-Xeno-Selected-In-Flight, X-Xeno-Selected-Max-Parallel, X-Xeno-Selected-Weight, X-Xeno-Selected-Last-Heartbeat-UTC, X-Xeno-Client-Roundtrip-Ms, X-Xeno-Request-Duration-Ms, X-Xeno-Request-History-ID, X-Xeno-Routing-Timeline, X-Xeno-Time-To-First-Token-Ms, X-Xeno-Total-Tokens, X-Xeno-Stream-Chunks";
        private const int MaxCapturedBodyChars = 131072;

        private readonly XenoSettings _Settings;
        private readonly string _SettingsFile;
        private readonly Webserver _Webserver;
        private readonly object _ClientConnectionsLock = new object();
        private readonly object _PendingResponsesLock = new object();
        private readonly object _PendingTelemetryResponsesLock = new object();
        private readonly object _AuthRateLimitLock = new object();
        private readonly Dictionary<string, ClientConnection> _ClientConnections = new Dictionary<string, ClientConnection>(StringComparer.OrdinalIgnoreCase);
        private readonly Dictionary<string, PendingClientResponse> _PendingClientResponses = new Dictionary<string, PendingClientResponse>(StringComparer.OrdinalIgnoreCase);
        private readonly Dictionary<string, PendingTelemetryResponse> _PendingTelemetryResponses = new Dictionary<string, PendingTelemetryResponse>(StringComparer.OrdinalIgnoreCase);
        private readonly Dictionary<string, List<DateTime>> _AuthRateLimitAttempts = new Dictionary<string, List<DateTime>>(StringComparer.OrdinalIgnoreCase);
        private readonly ConcurrentDictionary<string, CancellationTokenSource> _PendingCallerDisconnects = new ConcurrentDictionary<string, CancellationTokenSource>(StringComparer.OrdinalIgnoreCase);
        private readonly ConcurrentDictionary<string, byte> _ActiveNukeOperations = new ConcurrentDictionary<string, byte>(StringComparer.OrdinalIgnoreCase);
        private readonly ConcurrentDictionary<string, long> _EmailSentByProviderTemplate = new ConcurrentDictionary<string, long>(StringComparer.OrdinalIgnoreCase);
        private readonly ConcurrentDictionary<string, long> _EmailSkippedByProviderTemplate = new ConcurrentDictionary<string, long>(StringComparer.OrdinalIgnoreCase);
        private readonly ConcurrentDictionary<string, long> _EmailFailedByProviderTemplate = new ConcurrentDictionary<string, long>(StringComparer.OrdinalIgnoreCase);
        private readonly AuthenticationService _Authentication = new AuthenticationService();
        private readonly AuthorizationService _Authorization = new AuthorizationService();
        private readonly ClientTelemetryLatestCache _TelemetryLatestCache = new ClientTelemetryLatestCache();
        private readonly IXenoEmailService _Email;
        private DatabaseDriverBase? _Database;
        private RoutingService? _Routing;
        private LedgerService? _Ledger;
        private TenantLifecycleService? _TenantLifecycle;
        private NetLedgerTenantSubordinateService? _NetLedgerTenantSubordinate;
        private CancellationTokenSource? _TelemetryCollectionCancellation;
        private Task? _TelemetryCollectionTask;
        private CancellationTokenSource? _WebSocketMaintenanceCancellation;
        private Task? _WebSocketMaintenanceTask;
        private DateTime? _TelemetryLastCycleUtc;
        private DateTime? _TelemetryNextCycleUtc;
        private DateTime? _TelemetryNextPruneUtc;
        private int _TelemetryRecentFailureCount;
        private int _TelemetryCollectionCycleActive;
        private long _AuthSuccessCount;
        private long _AuthFailureCount;
        private long _AuthorizationDenialCount;
        private long _PrivilegedBypassCount;
        private long _EmailSentCount;
        private long _EmailSkippedCount;
        private long _EmailFailureCount;
        private long _EmailLastFailureTicks;
        private bool _Disposed;

        /// <summary>
        /// Instantiate the server.
        /// </summary>
        /// <param name="settings">Server settings.</param>
        /// <param name="settingsFile">Settings file path used for dashboard settings persistence.</param>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="settings"/> is null.</exception>
        public XenoServer(XenoSettings settings, string? settingsFile = null)
        {
            _Settings = settings ?? throw new ArgumentNullException(nameof(settings));
            _SettingsFile = String.IsNullOrWhiteSpace(settingsFile)
                ? Environment.GetEnvironmentVariable("XENO_SETTINGS_FILE") ?? "xeno.json"
                : settingsFile;
            ApplyEnvironmentSettings(_Settings);

            WatsonWebserver.Core.WebserverSettings webserverSettings = new WatsonWebserver.Core.WebserverSettings(
                _Settings.Rest.Hostname,
                _Settings.Rest.Port,
                _Settings.Rest.Ssl);
            webserverSettings.WebSockets.Enable = _Settings.WebSocket.Enabled;

            _Webserver = new Webserver(webserverSettings, DefaultRouteAsync);
            _Email = new XenoEmailService(_Settings.Email, EmailProviderFactory.Create(_Settings.Email));
            ConfigureWatson();
        }

        /// <summary>
        /// Start the server and run until cancellation.
        /// </summary>
        /// <param name="token">Cancellation token.</param>
        /// <returns>Task.</returns>
        public async Task StartAsync(CancellationToken token = default)
        {
            token.ThrowIfCancellationRequested();
            _Database = await DatabaseDriverFactory.CreateAndInitializeAsync(_Settings.Database, token).ConfigureAwait(false);
            MarkClientsDisconnected();
            _Routing = new RoutingService(_Database.Platform, _Settings.Routing);
            _Ledger = new LedgerService(_Database.Platform, _Settings.Ledger, SavePlatformAsync);
            _NetLedgerTenantSubordinate = new NetLedgerTenantSubordinateService(_Database.Platform, _Settings.Ledger);
            _TenantLifecycle = new TenantLifecycleService(_Database.Platform, new ITenantSubordinateService[] { _NetLedgerTenantSubordinate });

            _Webserver.Start();
            _Ledger.Start();
            StartTelemetryCollectionLoop();
            StartWebSocketMaintenanceLoop();

            try
            {
                while (!token.IsCancellationRequested)
                {
                    await Task.Delay(250, token).ConfigureAwait(false);
                }
            }
            catch (OperationCanceledException)
            {
            }
            finally
            {
                Stop();
            }
        }

        /// <summary>
        /// Stop the server.
        /// </summary>
        public void Stop()
        {
            _TelemetryCollectionCancellation?.Cancel();
            _WebSocketMaintenanceCancellation?.Cancel();
            _Ledger?.Stop();
            _NetLedgerTenantSubordinate?.Dispose();
            try
            {
                _Webserver.Stop();
            }
            catch (InvalidOperationException ex) when (ex.Message.Contains("already stopped", StringComparison.OrdinalIgnoreCase))
            {
            }
        }

        /// <summary>
        /// Dispose the server.
        /// </summary>
        public void Dispose()
        {
            if (_Disposed) return;
            Stop();
            IDisposable? disposable = _Webserver as IDisposable;
            disposable?.Dispose();
            _Ledger?.Dispose();
            _Database?.Dispose();
            _TelemetryLatestCache.Dispose();
            _Disposed = true;
        }

        private void ConfigureWatson()
        {
            _Webserver.Events.RequestAborted += HandleRequestAborted;
            _Webserver.Events.RequestorDisconnected += HandleRequestorDisconnected;

            _Webserver.UseOpenApi(openApi =>
            {
                openApi.EnableOpenApi = true;
                openApi.EnableSwaggerUi = false;
                openApi.Info.Title = "Xeno API";
                openApi.Info.Version = Constants.Version;
                openApi.Info.Description = "Xeno AI control plane REST and OpenAI-compatible API.";
                openApi.Info.Contact = new OpenApiContact
                {
                    Name = "Xeno Support",
                    Url = "https://github.com/jchristn/Xeno"
                };
                openApi.Info.License = new OpenApiLicense
                {
                    Name = "MIT",
                    Url = "https://opensource.org/licenses/MIT"
                };
                openApi.Tags.Add(new OpenApiTag { Name = "Health", Description = "Health check endpoints" });
                openApi.Tags.Add(new OpenApiTag { Name = "Authentication", Description = "Tenant-bound token endpoints" });
                openApi.Tags.Add(new OpenApiTag { Name = "Administration", Description = "Destructive administrative operations" });
                openApi.Tags.Add(new OpenApiTag { Name = "Tenants", Description = "Tenant administration" });
                openApi.Tags.Add(new OpenApiTag { Name = "Clients", Description = "Connected Xeno clients and their advertised models" });
                openApi.Tags.Add(new OpenApiTag { Name = "Client Telemetry", Description = "Server-collected client host and provider telemetry" });
                openApi.Tags.Add(new OpenApiTag { Name = "Routing", Description = "Direct routing to connected Xeno clients" });
                openApi.Tags.Add(new OpenApiTag { Name = "OpenAI", Description = "OpenAI-compatible inference API" });
                openApi.Tags.Add(new OpenApiTag { Name = "Ollama", Description = "Ollama-compatible inference API" });
                openApi.Tags.Add(new OpenApiTag { Name = "Request History", Description = "Captured request history and summaries" });
                openApi.Tags.Add(new OpenApiTag { Name = "Ledger", Description = "Token ledger status, balances, entries, and operations" });
                openApi.Tags.Add(new OpenApiTag { Name = "Model Leaderboard", Description = "Public model token usage leaderboards" });
                openApi.Tags.Add(new OpenApiTag { Name = "Settings", Description = "Server settings and JSON config persistence" });
                openApi.Tags.Add(new OpenApiTag { Name = "Email", Description = "Administrative email delivery tests" });
                AddXenoOpenApiSecuritySchemes(openApi.SecuritySchemes);
            });

            _Webserver.Routes.Preflight = PreflightRouteAsync;
            _Webserver.Routes.PostRouting = PostRoutingRouteAsync;
            _Webserver.Routes.PreAuthentication.Static.Add(HttpMethod.GET, "/openapi.json", OpenApiRouteAsync, ExceptionRouteAsync);
            _Webserver.Routes.PreAuthentication.Static.Add(HttpMethod.GET, "/swagger", SwaggerRouteAsync, ExceptionRouteAsync);
            RegisterRestRoutes();

            if (_Settings.WebSocket.Enabled)
            {
                _Webserver.WebSocket(_Settings.WebSocket.Path, WebSocketRouteAsync, false);
            }
        }

        private void HandleRequestAborted(object? sender, RequestEventArgs args)
        {
            CancelPendingRoutedRequest(args);
        }

        private void HandleRequestorDisconnected(object? sender, RequestEventArgs args)
        {
            CancelPendingRoutedRequest(args);
        }

        private static void AddXenoOpenApiSecuritySchemes(IDictionary<string, OpenApiSecurityScheme> schemes)
        {
            schemes["bearerAuth"] = new OpenApiSecurityScheme
            {
                Type = "http",
                Scheme = "bearer",
                BearerFormat = "Xeno token",
                Description = "Bearer session token or globally unique credential value. Bearer auth infers tenant context."
            };
            schemes["xTokenAuth"] = new OpenApiSecurityScheme
            {
                Type = "apiKey",
                In = "header",
                Name = "x-token",
                Description = "Xeno bearer token supplied as an x-token header."
            };
            schemes["accessKeyAuth"] = new OpenApiSecurityScheme
            {
                Type = "apiKey",
                In = "header",
                Name = "x-access-key",
                Description = "Tenant-bound Xeno credential access key."
            };
            schemes["tenantHeader"] = new OpenApiSecurityScheme
            {
                Type = "apiKey",
                In = "header",
                Name = "x-tenant-guid",
                Description = "Optional tenant selector or compatibility hint. Bearer auth infers tenant context when omitted."
            };
            schemes["tenantIdHeader"] = new OpenApiSecurityScheme
            {
                Type = "apiKey",
                In = "header",
                Name = "x-tenant-id",
                Description = "Alternate optional tenant identifier header."
            };
        }

        private void RegisterRestRoutes()
        {
            RegisterStaticRoute(HttpMethod.GET, "/", "Health check", "Health");
            RegisterStaticRoute(HttpMethod.GET, "/health", "Health check", "Health");
            RegisterStaticRoute(HttpMethod.GET, "/ping", "Ping heartbeat", "Health");
            RegisterStaticRoute(HttpMethod.GET, "/metrics", "Prometheus-style metrics", "Health");
            RegisterStaticRoute(HttpMethod.GET, "/v1.0/api/health", "Health check", "Health");
            RegisterStaticRoute(HttpMethod.GET, "/api/v1/ping", "Ping heartbeat", "Health");
            RegisterStaticRoute(HttpMethod.GET, "/v1.0/api/ping", "Ping heartbeat", "Health");
            RegisterStaticRoute(HttpMethod.GET, "/api/v1/auth/tenants", "List login tenants for email", "Authentication");
            RegisterStaticRoute(HttpMethod.GET, "/v1.0/api/auth/tenants", "List login tenants for email", "Authentication");
            RegisterStaticRoute(HttpMethod.POST, "/api/v1/auth/tenants", "List login tenants for email", "Authentication");
            RegisterStaticRoute(HttpMethod.POST, "/v1.0/api/auth/tenants", "List login tenants for email", "Authentication");
            RegisterStaticRoute(HttpMethod.POST, "/api/v1/auth/dashboard/login", "Dashboard login", "Authentication");
            RegisterStaticRoute(HttpMethod.POST, "/v1.0/api/auth/dashboard/login", "Dashboard login", "Authentication");
            RegisterStaticRoute(HttpMethod.POST, "/api/v1/onboarding/signup", "Onboard tenant or user", "Onboarding");
            RegisterStaticRoute(HttpMethod.POST, "/v1.0/api/onboarding/signup", "Onboard tenant or user", "Onboarding");
            RegisterStaticRoute(HttpMethod.POST, "/api/v1/auth/login", "Login and create client credential", "Authentication");
            RegisterStaticRoute(HttpMethod.POST, "/v1.0/api/auth/login", "Login and create client credential", "Authentication");
            RegisterStaticRoute(HttpMethod.POST, "/api/v1/auth/token", "Create token", "Authentication");
            RegisterStaticRoute(HttpMethod.POST, "/v1.0/api/auth/token", "Create token", "Authentication");
            RegisterStaticRoute(HttpMethod.GET, "/api/v1/auth/token/details", "Token details", "Authentication");
            RegisterStaticRoute(HttpMethod.GET, "/v1.0/api/auth/token/details", "Token details", "Authentication");
            RegisterStaticRoute(HttpMethod.POST, "/api/v1/auth/token/refresh", "Refresh token", "Authentication");
            RegisterStaticRoute(HttpMethod.POST, "/v1.0/api/auth/token/refresh", "Refresh token", "Authentication");
            RegisterStaticRoute(HttpMethod.POST, "/api/v1/auth/logout", "Logout", "Authentication");
            RegisterStaticRoute(HttpMethod.POST, "/v1.0/api/auth/logout", "Logout", "Authentication");
            RegisterStaticRoute(HttpMethod.POST, "/api/v1/admin/nuke/tenant", "Nuke tenant", "Administration");
            RegisterStaticRoute(HttpMethod.POST, "/v1.0/api/admin/nuke/tenant", "Nuke tenant", "Administration");
            RegisterStaticRoute(HttpMethod.POST, "/api/v1/admin/nuke/user", "Nuke user", "Administration");
            RegisterStaticRoute(HttpMethod.POST, "/v1.0/api/admin/nuke/user", "Nuke user", "Administration");
            RegisterStaticRoute(HttpMethod.GET, "/api/v1/tenants", "List tenants", "Tenants");
            RegisterStaticRoute(HttpMethod.GET, "/v1.0/api/tenants", "List tenants", "Tenants");
            RegisterStaticRoute(HttpMethod.POST, "/api/v1/tenants", "Create tenant", "Tenants");
            RegisterStaticRoute(HttpMethod.POST, "/v1.0/api/tenants", "Create tenant", "Tenants");
            RegisterStaticRoute(HttpMethod.GET, "/api/v1/users", "List users", "Tenants");
            RegisterStaticRoute(HttpMethod.GET, "/v1.0/api/users", "List users", "Tenants");
            RegisterStaticRoute(HttpMethod.POST, "/api/v1/users", "Create user", "Tenants");
            RegisterStaticRoute(HttpMethod.POST, "/v1.0/api/users", "Create user", "Tenants");
            RegisterStaticRoute(HttpMethod.GET, "/api/v1/credentials", "List credentials", "Tenants");
            RegisterStaticRoute(HttpMethod.GET, "/v1.0/api/credentials", "List credentials", "Tenants");
            RegisterStaticRoute(HttpMethod.POST, "/api/v1/credentials", "Create credential", "Tenants");
            RegisterStaticRoute(HttpMethod.POST, "/v1.0/api/credentials", "Create credential", "Tenants");
            RegisterStaticRoute(HttpMethod.GET, "/api/v1/roles", "List roles", "Authorization");
            RegisterStaticRoute(HttpMethod.GET, "/v1.0/api/roles", "List roles", "Authorization");
            RegisterStaticRoute(HttpMethod.POST, "/api/v1/roles", "Create role", "Authorization");
            RegisterStaticRoute(HttpMethod.POST, "/v1.0/api/roles", "Create role", "Authorization");
            RegisterStaticRoute(HttpMethod.GET, "/api/v1/permissions", "List permissions", "Authorization");
            RegisterStaticRoute(HttpMethod.GET, "/v1.0/api/permissions", "List permissions", "Authorization");
            RegisterStaticRoute(HttpMethod.GET, "/api/v1/permissions/grantable", "List grantable permissions", "Authorization");
            RegisterStaticRoute(HttpMethod.GET, "/v1.0/api/permissions/grantable", "List grantable permissions", "Authorization");
            RegisterStaticRoute(HttpMethod.POST, "/api/v1/permissions", "Create permission", "Authorization");
            RegisterStaticRoute(HttpMethod.POST, "/v1.0/api/permissions", "Create permission", "Authorization");
            RegisterStaticRoute(HttpMethod.GET, "/api/v1/roleassignments", "List role assignments", "Authorization");
            RegisterStaticRoute(HttpMethod.GET, "/v1.0/api/role-assignments", "List role assignments", "Authorization");
            RegisterStaticRoute(HttpMethod.POST, "/api/v1/roleassignments", "Create role assignment", "Authorization");
            RegisterStaticRoute(HttpMethod.POST, "/v1.0/api/role-assignments", "Create role assignment", "Authorization");
            RegisterStaticRoute(HttpMethod.GET, "/api/v1/effective-permissions", "List effective permissions", "Authorization");
            RegisterStaticRoute(HttpMethod.GET, "/v1.0/api/effective-permissions", "List effective permissions", "Authorization");
            RegisterStaticRoute(HttpMethod.GET, "/api/v1/audit", "List audit records", "Audit");
            RegisterStaticRoute(HttpMethod.GET, "/v1.0/api/audit", "List audit records", "Audit");
            RegisterStaticRoute(HttpMethod.GET, "/api/v1/clients", "List clients", "Clients");
            RegisterStaticRoute(HttpMethod.GET, "/v1.0/api/clients", "List clients", "Clients");
            RegisterStaticRoute(HttpMethod.DELETE, "/api/v1/clientmodels", "Delete client-advertised model", "Clients");
            RegisterStaticRoute(HttpMethod.DELETE, "/v1.0/api/client-models", "Delete client-advertised model", "Clients");
            RegisterStaticRoute(HttpMethod.GET, "/v1.0/api/client-telemetry/status", "Client telemetry status", "Client Telemetry");
            RegisterStaticRoute(HttpMethod.GET, "/v1.0/api/client-telemetry/latest", "Latest client telemetry", "Client Telemetry");
            RegisterStaticRoute(HttpMethod.POST, "/v1.0/api/client-telemetry/collect", "Trigger client telemetry collection", "Client Telemetry");
            RegisterStaticRoute(HttpMethod.POST, "/v1.0/api/client-telemetry/history/search", "Search client telemetry history", "Client Telemetry");
            RegisterStaticRoute(HttpMethod.POST, "/v1.0/api/client-telemetry/history/rollups", "Client telemetry rollups", "Client Telemetry");
            RegisterStaticRoute(HttpMethod.POST, "/v1.0/api/client-telemetry/history/delete", "Bulk delete client telemetry", "Client Telemetry");
            RegisterStaticRoute(HttpMethod.GET, "/v1.0/api/client-telemetry/providers/latest", "Latest provider telemetry", "Client Telemetry");
            RegisterStaticRoute(HttpMethod.POST, "/v1.0/api/client-telemetry/providers/collect", "Trigger provider telemetry collection", "Client Telemetry");
            RegisterStaticRoute(HttpMethod.POST, "/v1.0/api/client-telemetry/providers/history/search", "Search provider telemetry history", "Client Telemetry");
            RegisterStaticRoute(HttpMethod.POST, "/v1.0/api/client-telemetry/providers/history/rollups", "Provider telemetry rollups", "Client Telemetry");
            RegisterStaticRoute(HttpMethod.POST, "/v1.0/api/client-telemetry/providers/history/delete", "Bulk delete provider telemetry", "Client Telemetry");
            RegisterStaticRoute(HttpMethod.GET, "/v1.0/api/client-telemetry/collection-attempts", "List client telemetry collection attempts", "Client Telemetry");
            RegisterStaticRoute(HttpMethod.GET, "/v1.0/api/client-telemetry/metrics", "Client telemetry metric catalog", "Client Telemetry");
            RegisterStaticRoute(HttpMethod.GET, "/v1.0/api/ledger/status", "Ledger status", "Ledger");
            RegisterStaticRoute(HttpMethod.GET, "/v1.0/api/ledger/balance/me", "My ledger balance", "Ledger");
            RegisterStaticRoute(HttpMethod.GET, "/v1.0/api/ledger/balances", "Ledger balances", "Ledger");
            RegisterStaticRoute(HttpMethod.GET, "/v1.0/api/ledger/entries/me", "My ledger entries", "Ledger");
            RegisterStaticRoute(HttpMethod.POST, "/v1.0/api/ledger/entries/search", "Search ledger entries", "Ledger");
            RegisterStaticRoute(HttpMethod.POST, "/v1.0/api/ledger/drain", "Drain ledger queue", "Ledger");
            RegisterStaticRoute(HttpMethod.POST, "/v1.0/api/ledger/reconcile", "Reconcile ledger links", "Ledger");
            RegisterStaticRoute(HttpMethod.GET, "/api/v1/modeldefinitions", "List model definitions", "Routing");
            RegisterStaticRoute(HttpMethod.GET, "/v1.0/api/model-definitions", "List model definitions", "Routing");
            RegisterStaticRoute(HttpMethod.GET, "/api/v1/modelconfigurations", "List model configurations", "Routing");
            RegisterStaticRoute(HttpMethod.GET, "/v1.0/api/model-configurations", "List model configurations", "Routing");
            RegisterStaticRoute(HttpMethod.GET, "/api/v1/routingpolicies", "List routing policies", "Routing");
            RegisterStaticRoute(HttpMethod.GET, "/v1.0/api/routing-policies", "List routing policies", "Routing");
            RegisterStaticRoute(HttpMethod.POST, "/api/v1/routingpolicies", "Create routing policy", "Routing");
            RegisterStaticRoute(HttpMethod.POST, "/v1.0/api/routing-policies", "Create routing policy", "Routing");
            RegisterStaticRoute(HttpMethod.POST, "/v1.0/api/routing-policies/validate", "Validate routing policy", "Routing");
            RegisterStaticRoute(HttpMethod.GET, "/v1/models", "List OpenAI models", "OpenAI");
            RegisterStaticRoute(HttpMethod.GET, "/api/v1/openai/v1/models", "List OpenAI models", "OpenAI");
            RegisterStaticRoute(HttpMethod.POST, "/v1/chat/completions", "Create chat completion", "OpenAI");
            RegisterStaticRoute(HttpMethod.POST, "/v1/completions", "Create completion", "OpenAI");
            RegisterStaticRoute(HttpMethod.POST, "/api/v1/openai/v1/chat/completions", "Create chat completion", "OpenAI");
            RegisterStaticRoute(HttpMethod.POST, "/api/v1/openai/v1/completions", "Create completion", "OpenAI");
            RegisterStaticRoute(HttpMethod.POST, "/v1/embeddings", "Create embeddings", "OpenAI");
            RegisterStaticRoute(HttpMethod.POST, "/api/v1/openai/v1/embeddings", "Create embeddings", "OpenAI");
            RegisterStaticRoute(HttpMethod.GET, "/api/tags", "List Ollama models", "Ollama");
            RegisterStaticRoute(HttpMethod.POST, "/api/chat", "Create Ollama chat completion", "Ollama");
            RegisterStaticRoute(HttpMethod.POST, "/api/generate", "Create Ollama generation", "Ollama");
            RegisterStaticRoute(HttpMethod.POST, "/api/embed", "Create Ollama embeddings", "Ollama");
            RegisterStaticRoute(HttpMethod.POST, "/api/embeddings", "Create Ollama embeddings", "Ollama");
            RegisterStaticRoute(HttpMethod.GET, "/api/v1/requesthistory", "List request history", "Request History");
            RegisterStaticRoute(HttpMethod.GET, "/v1.0/api/request-history", "List request history", "Request History");
            RegisterStaticRoute(HttpMethod.GET, "/api/v1/modelleaderboard", "Model token leaderboard", "Model Leaderboard");
            RegisterStaticRoute(HttpMethod.GET, "/v1.0/api/request-history/model-leaderboard", "Model token leaderboard", "Model Leaderboard");
            RegisterStaticRoute(HttpMethod.POST, "/api/v1/modelleaderboard", "Model token leaderboard", "Model Leaderboard");
            RegisterStaticRoute(HttpMethod.POST, "/v1.0/api/request-history/model-leaderboard", "Model token leaderboard", "Model Leaderboard");
            RegisterStaticRoute(HttpMethod.GET, "/v1.0/api/request-history/summary", "Request history summary", "Request History");
            RegisterStaticRoute(HttpMethod.GET, "/api/v1/requesthistory/summary", "Request history summary", "Request History");
            RegisterStaticRoute(HttpMethod.POST, "/api/v1/requesthistory/bulk", "Bulk delete request history", "Request History");
            RegisterStaticRoute(HttpMethod.POST, "/v1.0/api/request-history/bulk", "Bulk delete request history", "Request History");
            RegisterStaticRoute(HttpMethod.GET, "/api/v1/requesthistory/analytics/overview", "Request analytics overview", "Request History");
            RegisterStaticRoute(HttpMethod.GET, "/v1.0/api/request-history/analytics/overview", "Request analytics overview", "Request History");
            RegisterStaticRoute(HttpMethod.GET, "/api/v1/analytics/overview", "Analytics overview", "Analytics");
            RegisterStaticRoute(HttpMethod.GET, "/v1.0/api/analytics/overview", "Analytics overview", "Analytics");
            RegisterStaticRoute(HttpMethod.GET, "/api/v1/settings", "Get server settings", "Settings");
            RegisterStaticRoute(HttpMethod.GET, "/v1.0/api/settings", "Get server settings", "Settings");
            RegisterStaticRoute(HttpMethod.POST, "/api/v1/settings", "Update server settings", "Settings");
            RegisterStaticRoute(HttpMethod.POST, "/v1.0/api/settings", "Update server settings", "Settings");
            RegisterStaticRoute(HttpMethod.POST, "/api/v1/email/test", "Send test email", "Email");
            RegisterStaticRoute(HttpMethod.POST, "/v1.0/api/email/test", "Send test email", "Email");
            RegisterStaticRoute(HttpMethod.GET, "/api/v1/backup", "Backup platform state", "Backup");
            RegisterStaticRoute(HttpMethod.GET, "/v1.0/api/backup", "Backup platform state", "Backup");
            RegisterStaticRoute(HttpMethod.POST, "/api/v1/restore", "Restore platform state", "Backup");
            RegisterStaticRoute(HttpMethod.POST, "/v1.0/api/restore", "Restore platform state", "Backup");
            RegisterParameterizedRoute(HttpMethod.DELETE, "/api/v1/tenants/{id}", "Delete tenant", "Tenants");
            RegisterParameterizedRoute(HttpMethod.DELETE, "/v1.0/api/tenants/{id}", "Delete tenant", "Tenants");
            RegisterParameterizedRoute(HttpMethod.PUT, "/api/v1/tenants/{id}", "Update tenant", "Tenants");
            RegisterParameterizedRoute(HttpMethod.PUT, "/v1.0/api/tenants/{id}", "Update tenant", "Tenants");
            RegisterParameterizedRoute(HttpMethod.DELETE, "/api/v1/users/{id}", "Delete user", "Tenants");
            RegisterParameterizedRoute(HttpMethod.DELETE, "/v1.0/api/users/{id}", "Delete user", "Tenants");
            RegisterParameterizedRoute(HttpMethod.PUT, "/api/v1/users/{id}", "Update user", "Tenants");
            RegisterParameterizedRoute(HttpMethod.PUT, "/v1.0/api/users/{id}", "Update user", "Tenants");
            RegisterParameterizedRoute(HttpMethod.DELETE, "/api/v1/credentials/{id}", "Delete credential", "Tenants");
            RegisterParameterizedRoute(HttpMethod.DELETE, "/v1.0/api/credentials/{id}", "Delete credential", "Tenants");
            RegisterParameterizedRoute(HttpMethod.PUT, "/api/v1/credentials/{id}", "Update credential", "Tenants");
            RegisterParameterizedRoute(HttpMethod.PUT, "/v1.0/api/credentials/{id}", "Update credential", "Tenants");
            RegisterParameterizedRoute(HttpMethod.DELETE, "/api/v1/roles/{id}", "Delete role", "Authorization");
            RegisterParameterizedRoute(HttpMethod.DELETE, "/v1.0/api/roles/{id}", "Delete role", "Authorization");
            RegisterParameterizedRoute(HttpMethod.PUT, "/api/v1/roles/{id}", "Update role", "Authorization");
            RegisterParameterizedRoute(HttpMethod.PUT, "/v1.0/api/roles/{id}", "Update role", "Authorization");
            RegisterParameterizedRoute(HttpMethod.DELETE, "/api/v1/permissions/{id}", "Delete permission", "Authorization");
            RegisterParameterizedRoute(HttpMethod.DELETE, "/v1.0/api/permissions/{id}", "Delete permission", "Authorization");
            RegisterParameterizedRoute(HttpMethod.PUT, "/api/v1/permissions/{id}", "Update permission", "Authorization");
            RegisterParameterizedRoute(HttpMethod.PUT, "/v1.0/api/permissions/{id}", "Update permission", "Authorization");
            RegisterParameterizedRoute(HttpMethod.DELETE, "/api/v1/roleassignments/{id}", "Delete role assignment", "Authorization");
            RegisterParameterizedRoute(HttpMethod.DELETE, "/v1.0/api/role-assignments/{id}", "Delete role assignment", "Authorization");
            RegisterParameterizedRoute(HttpMethod.DELETE, "/api/v1/audit/{id}", "Delete audit record", "Audit");
            RegisterParameterizedRoute(HttpMethod.DELETE, "/v1.0/api/audit/{id}", "Delete audit record", "Audit");
            RegisterParameterizedRoute(HttpMethod.DELETE, "/api/v1/clients/{id}", "Delete client", "Clients");
            RegisterParameterizedRoute(HttpMethod.DELETE, "/v1.0/api/clients/{id}", "Delete client", "Clients");
            RegisterParameterizedRoute(HttpMethod.GET, "/v1.0/api/client-telemetry/history/{id}", "Get client telemetry detail", "Client Telemetry");
            RegisterParameterizedRoute(HttpMethod.DELETE, "/v1.0/api/client-telemetry/history/{id}", "Delete client telemetry detail", "Client Telemetry");
            RegisterParameterizedRoute(HttpMethod.GET, "/v1.0/api/client-telemetry/providers/history/{id}", "Get provider telemetry detail", "Client Telemetry");
            RegisterParameterizedRoute(HttpMethod.DELETE, "/v1.0/api/client-telemetry/providers/history/{id}", "Delete provider telemetry detail", "Client Telemetry");
            RegisterParameterizedRoute(HttpMethod.GET, "/v1.0/api/ledger/balances/users/{userId}", "Ledger user balance", "Ledger");
            RegisterParameterizedRoute(HttpMethod.PUT, "/api/v1/routingpolicies/{id}", "Update routing policy", "Routing");
            RegisterParameterizedRoute(HttpMethod.PUT, "/v1.0/api/routing-policies/{id}", "Update routing policy", "Routing");
            RegisterParameterizedRoute(HttpMethod.DELETE, "/api/v1/routingpolicies/{id}", "Delete routing policy", "Routing");
            RegisterParameterizedRoute(HttpMethod.DELETE, "/v1.0/api/routing-policies/{id}", "Delete routing policy", "Routing");
            RegisterParameterizedRoute(HttpMethod.GET, "/api/v1/requesthistory/{id}", "Get request history entry", "Request History");
            RegisterParameterizedRoute(HttpMethod.GET, "/v1.0/api/request-history/{id}", "Get request history entry", "Request History");
            RegisterParameterizedRoute(HttpMethod.DELETE, "/api/v1/requesthistory/{id}", "Delete request history entry", "Request History");
            RegisterParameterizedRoute(HttpMethod.DELETE, "/v1.0/api/request-history/{id}", "Delete request history entry", "Request History");
        }

        private void RegisterStaticRoute(HttpMethod method, string path, string summary, string tag)
        {
            _Webserver.Routes.PreAuthentication.Static.Add(
                method,
                path,
                DefaultRouteAsync,
                ExceptionRouteAsync,
                openApiMetadata: BuildRouteMetadata(method, path, summary, tag));
        }

        private void RegisterParameterizedRoute(HttpMethod method, string path, string summary, string tag)
        {
            _Webserver.Routes.PreAuthentication.Parameter.Add(
                method,
                path,
                DefaultRouteAsync,
                ExceptionRouteAsync,
                openApiMetadata: BuildRouteMetadata(method, path, summary, tag));
        }

        private static OpenApiRouteMetadata BuildRouteMetadata(HttpMethod method, string path, string summary, string tag)
        {
            OpenApiRouteMetadata metadata = OpenApiRouteMetadata.Create(summary, tag)
                .WithDescription(summary + ".");

            foreach (string parameterName in ExtractPathParameterNames(path))
            {
                metadata = metadata.WithParameter(OpenApiParameterMetadata.Path(parameterName, "Route identifier.", OpenApiSchemaMetadata.String("Identifier.")));
            }

            if (SupportsTenantQuery(path))
            {
                metadata = metadata.WithParameter(OpenApiParameterMetadata.Query("tenantId", "Optional tenant identifier. Defaults to ten_default.", false, OpenApiSchemaMetadata.String("Tenant identifier.")));
            }

            if (IsPath(path, "/ping", "/api/v1/ping", "/v1.0/api/ping"))
            {
                metadata = metadata
                    .WithParameter(OpenApiParameterMetadata.Query("sequence", "Optional client sequence number echoed by the server.", false, IntegerSchemaMetadata("Sequence number.")))
                    .WithParameter(OpenApiParameterMetadata.Query("clientSentUtc", "Optional UTC timestamp echoed by the server.", false, DateTimeSchema("Client sent timestamp.")));
            }

            if (IsPath(path, "/api/v1/requesthistory/summary", "/v1.0/api/request-history/summary"))
            {
                metadata = metadata
                    .WithParameter(OpenApiParameterMetadata.Query("fromUtc", "Inclusive UTC start time.", false, DateTimeSchema("Inclusive UTC start time.")))
                    .WithParameter(OpenApiParameterMetadata.Query("toUtc", "Exclusive UTC end time.", false, DateTimeSchema("Exclusive UTC end time.")))
                    .WithParameter(OpenApiParameterMetadata.Query("bucketMinutes", "Bucket width in minutes.", false, IntegerSchemaMetadata("Bucket width in minutes.")));
            }

            if (IsPath(path, "/api/v1/requesthistory", "/v1.0/api/request-history"))
            {
                metadata = metadata
                    .WithParameter(OpenApiParameterMetadata.Query("page", "One-based page number. Defaults to 1.", false, IntegerSchemaMetadata("Page number.")))
                    .WithParameter(OpenApiParameterMetadata.Query("pageSize", "Number of entries per page. Defaults to 100 and is capped at 1000.", false, IntegerSchemaMetadata("Page size.")))
                    .WithParameter(OpenApiParameterMetadata.Query("fromUtc", "Inclusive UTC start time.", false, DateTimeSchema("Inclusive UTC start time.")))
                    .WithParameter(OpenApiParameterMetadata.Query("toUtc", "Exclusive UTC end time.", false, DateTimeSchema("Exclusive UTC end time.")));
            }

            if (IsPath(path, "/api/v1/modelleaderboard", "/v1.0/api/request-history/model-leaderboard"))
            {
                metadata = metadata
                    .WithParameter(OpenApiParameterMetadata.Query("tenantId", "Optional tenant identifier. When omitted, global model usage is returned.", false, OpenApiSchemaMetadata.String("Tenant identifier.")))
                    .WithParameter(OpenApiParameterMetadata.Query("fromUtc", "Inclusive UTC start time.", false, DateTimeSchema("Inclusive UTC start time.")))
                    .WithParameter(OpenApiParameterMetadata.Query("toUtc", "Exclusive UTC end time.", false, DateTimeSchema("Exclusive UTC end time.")))
                    .WithParameter(OpenApiParameterMetadata.Query("limit", "Maximum ranked models to return. Omitted or null defaults to 100 and is capped at 1000.", false, IntegerSchemaMetadata("Result limit.")))
                    .WithParameter(OpenApiParameterMetadata.Query("offset", "Number of ranked models to skip. Omitted or null defaults to 0.", false, IntegerSchemaMetadata("Result offset.")));
            }

            if (IsPath(path, "/api/v1/requesthistory/analytics/overview", "/v1.0/api/request-history/analytics/overview", "/api/v1/analytics/overview", "/v1.0/api/analytics/overview"))
            {
                metadata = metadata
                    .WithParameter(OpenApiParameterMetadata.Query("fromUtc", "Inclusive UTC start time.", false, DateTimeSchema("Inclusive UTC start time.")))
                    .WithParameter(OpenApiParameterMetadata.Query("toUtc", "Exclusive UTC end time.", false, DateTimeSchema("Exclusive UTC end time.")));
            }

            if (IsPath(path, "/v1.0/api/ledger/balances"))
            {
                metadata = metadata
                    .WithParameter(OpenApiParameterMetadata.Query("userId", "Optional Xeno user identifier.", false, OpenApiSchemaMetadata.String("User identifier.")))
                    .WithParameter(OpenApiParameterMetadata.Query("page", "One-based page number. Defaults to 1.", false, IntegerSchemaMetadata("Page number.")))
                    .WithParameter(OpenApiParameterMetadata.Query("pageSize", "Number of balances per page. Defaults to 100 and is capped at 1000.", false, IntegerSchemaMetadata("Page size.")));
            }

            if (IsPath(path, "/api/v1/auth/tenants", "/v1.0/api/auth/tenants"))
            {
                metadata = metadata.WithParameter(OpenApiParameterMetadata.Query("email", "Email address used to discover tenant login options.", true, OpenApiSchemaMetadata.String("User email address.")));
            }

            OpenApiRequestBodyMetadata? requestBody = BuildRequestBodyMetadata(method, path);
            if (requestBody != null) metadata = metadata.WithRequestBody(requestBody);

            metadata = metadata
                .WithResponse(200, OpenApiResponseMetadata.Json("OK", OpenApiSchemaMetadata.Create("object", "JSON response.")))
                .WithResponse(400, OpenApiResponseMetadata.BadRequest(OpenApiSchemaMetadata.Create("object", "Error response.")))
                .WithResponse(401, OpenApiResponseMetadata.Unauthorized())
                .WithResponse(500, OpenApiResponseMetadata.InternalError(OpenApiSchemaMetadata.Create("object", "Error response.")));
            if (IsPath(path, "/api/v1/onboarding/signup", "/v1.0/api/onboarding/signup"))
            {
                metadata = metadata
                    .WithResponse(404, OpenApiResponseMetadata.Json("Tenant not found.", OpenApiSchemaMetadata.Create("object", "Error response.")))
                    .WithResponse(409, OpenApiResponseMetadata.Json("Tenant or user conflict.", OpenApiSchemaMetadata.Create("object", "Error response.")))
                    .WithResponse(429, OpenApiResponseMetadata.Json("Rate limited.", OpenApiSchemaMetadata.Create("object", "Error response.")));
            }

            return metadata;
        }

        private static IEnumerable<string> ExtractPathParameterNames(string path)
        {
            int searchIndex = 0;
            while (searchIndex < path.Length)
            {
                int start = path.IndexOf('{', searchIndex);
                if (start < 0) yield break;
                int end = path.IndexOf('}', start + 1);
                if (end < 0) yield break;
                string name = path.Substring(start + 1, end - start - 1).Trim();
                if (!String.IsNullOrWhiteSpace(name)) yield return name;
                searchIndex = end + 1;
            }
        }

        private static bool SupportsTenantQuery(string path)
        {
            return !IsPath(path, "/", "/health", "/ping", "/metrics", "/openapi.json", "/swagger", "/v1.0/api/health", "/api/v1/ping", "/v1.0/api/ping", "/api/v1/modelleaderboard", "/v1.0/api/request-history/model-leaderboard");
        }

        private static OpenApiRequestBodyMetadata? BuildRequestBodyMetadata(HttpMethod method, string path)
        {
            if (method == HttpMethod.POST && IsPath(path, "/api/v1/auth/login", "/v1.0/api/auth/login"))
            {
                return JsonBody(
                    "User login request. The password is exchanged for a client credential and is not stored by the Xeno client.",
                    ObjectSchemaMetadata(
                        "Login request.",
                        new[] { "username", "password" },
                        SchemaProperty("tenantId", OpenApiSchemaMetadata.String("Tenant identifier.")),
                        SchemaProperty("username", OpenApiSchemaMetadata.String("Username or email address.")),
                        SchemaProperty("email", OpenApiSchemaMetadata.String("Optional email alias for username.")),
                        SchemaProperty("password", OpenApiSchemaMetadata.String("User password."))),
                    new Dictionary<string, object>
                    {
                        ["tenantId"] = Constants.DefaultTenantId,
                        ["username"] = "admin",
                        ["password"] = "password"
                    });
            }

            if (method == HttpMethod.POST && IsPath(path, "/api/v1/auth/tenants", "/v1.0/api/auth/tenants"))
            {
                return JsonBody(
                    "Tenant lookup request. The email address is used to discover tenant login options.",
                    ObjectSchemaMetadata(
                        "Tenant lookup request.",
                        new[] { "email" },
                        SchemaProperty("email", OpenApiSchemaMetadata.String("User email address."))),
                    new Dictionary<string, object>
                    {
                        ["email"] = "admin@xeno"
                    });
            }

            if (method == HttpMethod.POST && IsPath(path, "/api/v1/auth/dashboard/login", "/v1.0/api/auth/dashboard/login"))
            {
                return JsonBody(
                    "Dashboard login request. The password is exchanged for a bearer session used by the dashboard.",
                    ObjectSchemaMetadata(
                        "Dashboard login request.",
                        new[] { "tenantId", "email", "password" },
                        SchemaProperty("tenantId", OpenApiSchemaMetadata.String("Tenant identifier.")),
                        SchemaProperty("email", OpenApiSchemaMetadata.String("User email address.")),
                        SchemaProperty("password", OpenApiSchemaMetadata.String("User password."))),
                    new Dictionary<string, object>
                    {
                        ["tenantId"] = Constants.DefaultTenantId,
                        ["email"] = "admin@xeno",
                        ["password"] = "password"
                    });
            }

            if (method == HttpMethod.POST && IsPath(path, "/api/v1/onboarding/signup", "/v1.0/api/onboarding/signup"))
            {
                return JsonBody(
                    "Onboarding request. Creates a tenant and first tenant administrator when tenantId is omitted, or adds a regular user to an existing tenant when tenantId is supplied.",
                    ObjectSchemaMetadata(
                        "Onboarding signup request.",
                        new[] { "email", "password" },
                        SchemaProperty("tenantId", OpenApiSchemaMetadata.String("Optional existing tenant identifier. Omit to create a new tenant.")),
                        SchemaProperty("tenantName", OpenApiSchemaMetadata.String("Tenant display name. Required when tenantId is omitted.")),
                        SchemaProperty("email", OpenApiSchemaMetadata.String("User email address.")),
                        SchemaProperty("username", OpenApiSchemaMetadata.String("Optional username. Defaults to email.")),
                        SchemaProperty("password", OpenApiSchemaMetadata.String("Initial password."))),
                    new Dictionary<string, object>
                    {
                        ["tenantName"] = "Example Company",
                        ["email"] = "admin@example.com",
                        ["password"] = "password"
                    });
            }

            if (method == HttpMethod.POST && IsPath(path, "/api/v1/auth/token", "/v1.0/api/auth/token"))
            {
                return JsonBody(
                    "Credential token request. The access key may also be supplied as an x-access-key header.",
                    ObjectSchemaMetadata(
                        "Credential token request.",
                        new[] { "accessKey" },
                        SchemaProperty("accessKey", OpenApiSchemaMetadata.String("Client access key."))),
                    new Dictionary<string, object>
                    {
                        ["accessKey"] = "default"
                    });
            }

            if (method == HttpMethod.POST && IsPath(path, "/api/v1/tenants", "/v1.0/api/tenants"))
            {
                return JsonBody(
                    "Tenant create request.",
                    ObjectSchemaMetadata(
                        "Tenant create request.",
                        new[] { "name" },
                        SchemaProperty("id", OpenApiSchemaMetadata.String("Optional tenant ID.")),
                        SchemaProperty("name", OpenApiSchemaMetadata.String("Tenant display name.")),
                        SchemaProperty("active", BooleanSchemaMetadata("Whether the tenant is active."))),
                    new Dictionary<string, object>
                    {
                        ["name"] = "Example Tenant",
                        ["active"] = true
                    });
            }

            if (method == HttpMethod.PUT && path.Contains("/tenants/{id}", StringComparison.OrdinalIgnoreCase))
            {
                return JsonBody(
                    "Tenant update request.",
                    ObjectSchemaMetadata(
                        "Tenant update request.",
                        Array.Empty<string>(),
                        SchemaProperty("name", OpenApiSchemaMetadata.String("Tenant display name.")),
                        SchemaProperty("active", BooleanSchemaMetadata("Whether the tenant is active."))),
                    new Dictionary<string, object>
                    {
                        ["name"] = "Example Tenant",
                        ["active"] = true
                    });
            }

            if (method == HttpMethod.POST && IsPath(path, "/api/v1/users", "/v1.0/api/users"))
            {
                return JsonBody(
                    "User create request.",
                    ObjectSchemaMetadata(
                        "User create request.",
                        new[] { "username", "password" },
                        SchemaProperty("tenantId", OpenApiSchemaMetadata.String("Tenant identifier.")),
                        SchemaProperty("username", OpenApiSchemaMetadata.String("Username.")),
                        SchemaProperty("email", OpenApiSchemaMetadata.String("Email address.")),
                        SchemaProperty("password", OpenApiSchemaMetadata.String("Initial password.")),
                        SchemaProperty("isAdmin", BooleanSchemaMetadata("Whether the user is a global administrator.")),
                        SchemaProperty("isTenantAdmin", BooleanSchemaMetadata("Whether the user is a tenant administrator.")),
                        SchemaProperty("active", BooleanSchemaMetadata("Whether the user is active."))),
                    new Dictionary<string, object>
                    {
                        ["username"] = "operator",
                        ["email"] = "operator@example.com",
                        ["password"] = "password",
                        ["isAdmin"] = false,
                        ["isTenantAdmin"] = false,
                        ["active"] = true
                    });
            }

            if (method == HttpMethod.PUT && path.Contains("/users/{id}", StringComparison.OrdinalIgnoreCase))
            {
                return JsonBody(
                    "User update request.",
                    ObjectSchemaMetadata(
                        "User update request.",
                        Array.Empty<string>(),
                        SchemaProperty("username", OpenApiSchemaMetadata.String("Username.")),
                        SchemaProperty("email", OpenApiSchemaMetadata.String("Email address.")),
                        SchemaProperty("password", OpenApiSchemaMetadata.String("Optional replacement password.")),
                        SchemaProperty("isAdmin", BooleanSchemaMetadata("Whether the user is a global administrator.")),
                        SchemaProperty("isTenantAdmin", BooleanSchemaMetadata("Whether the user is a tenant administrator.")),
                        SchemaProperty("active", BooleanSchemaMetadata("Whether the user is active."))),
                    new Dictionary<string, object>
                    {
                        ["email"] = "operator@example.com",
                        ["active"] = true
                    });
            }

            if (method == HttpMethod.POST && IsPath(path, "/api/v1/credentials", "/v1.0/api/credentials"))
            {
                return JsonBody(
                    "Credential create request.",
                    ObjectSchemaMetadata(
                        "Credential create request.",
                        Array.Empty<string>(),
                        SchemaProperty("tenantId", OpenApiSchemaMetadata.String("Tenant identifier.")),
                        SchemaProperty("userId", OpenApiSchemaMetadata.String("Owning user ID.")),
                        SchemaProperty("accessKey", OpenApiSchemaMetadata.String("Optional access key. Generated when omitted.")),
                        SchemaProperty("active", BooleanSchemaMetadata("Whether the credential is active."))),
                    new Dictionary<string, object>
                    {
                        ["userId"] = "usr_example",
                        ["accessKey"] = "default",
                        ["active"] = true
                    });
            }

            if (method == HttpMethod.PUT && path.Contains("/credentials/{id}", StringComparison.OrdinalIgnoreCase))
            {
                return JsonBody(
                    "Credential update request.",
                    ObjectSchemaMetadata(
                        "Credential update request.",
                        Array.Empty<string>(),
                        SchemaProperty("userId", OpenApiSchemaMetadata.String("Owning user ID.")),
                        SchemaProperty("accessKey", OpenApiSchemaMetadata.String("Access key.")),
                        SchemaProperty("active", BooleanSchemaMetadata("Whether the credential is active."))),
                    new Dictionary<string, object>
                    {
                        ["active"] = true
                    });
            }

            if (method == HttpMethod.POST && IsPath(path, "/api/v1/roles", "/v1.0/api/roles"))
            {
                return JsonBody(
                    "Role create request.",
                    RoleBodySchema("Role create request.", true),
                    new Dictionary<string, object>
                    {
                        ["name"] = "Operators",
                        ["permissionIds"] = Array.Empty<string>(),
                        ["active"] = true
                    });
            }

            if (method == HttpMethod.PUT && path.Contains("/roles/{id}", StringComparison.OrdinalIgnoreCase))
            {
                return JsonBody(
                    "Role update request.",
                    RoleBodySchema("Role update request.", false),
                    new Dictionary<string, object>
                    {
                        ["name"] = "Operators",
                        ["permissionIds"] = Array.Empty<string>(),
                        ["active"] = true
                    });
            }

            if (method == HttpMethod.POST && IsPath(path, "/api/v1/permissions", "/v1.0/api/permissions"))
            {
                return JsonBody(
                    "Permission create request.",
                    PermissionBodySchema("Permission create request.", true),
                    new Dictionary<string, object>
                    {
                        ["resourceType"] = "Clients",
                        ["operationType"] = "Read",
                        ["permit"] = true,
                        ["active"] = true
                    });
            }

            if (method == HttpMethod.PUT && path.Contains("/permissions/{id}", StringComparison.OrdinalIgnoreCase))
            {
                return JsonBody(
                    "Permission update request.",
                    PermissionBodySchema("Permission update request.", false),
                    new Dictionary<string, object>
                    {
                        ["resourceType"] = "Clients",
                        ["operationType"] = "Read",
                        ["permit"] = true,
                        ["active"] = true
                    });
            }

            if (method == HttpMethod.POST && IsPath(path, "/api/v1/routingpolicies", "/v1.0/api/routing-policies"))
            {
                return JsonBody(
                    "Routing policy create request. Use '*' for modelName or clientName to match all values.",
                    RoutingPolicyBodySchema("Routing policy create request.", true),
                    RoutingPolicyExample());
            }

            if (method == HttpMethod.POST && IsPath(path, "/v1.0/api/routing-policies/validate"))
            {
                return JsonBody(
                    "Routing policy validation request.",
                    RoutingPolicyBodySchema("Routing policy validation request.", false),
                    RoutingPolicyExample());
            }

            if (method == HttpMethod.PUT && path.Contains("/routing", StringComparison.OrdinalIgnoreCase) && path.Contains("{id}", StringComparison.OrdinalIgnoreCase))
            {
                return JsonBody(
                    "Routing policy update request. The tenant default policy keeps the */* match and cannot be disabled.",
                    RoutingPolicyBodySchema("Routing policy update request.", false),
                    RoutingPolicyExample());
            }

            if (method == HttpMethod.DELETE && IsPath(path, "/api/v1/clientmodels", "/v1.0/api/client-models"))
            {
                return JsonBody(
                    "Client-advertised model delete request.",
                    ObjectSchemaMetadata(
                        "Client-advertised model delete request.",
                        new[] { "clientId", "name" },
                        SchemaProperty("clientId", OpenApiSchemaMetadata.String("Client ID.")),
                        SchemaProperty("name", OpenApiSchemaMetadata.String("Model name.")),
                        SchemaProperty("source", OpenApiSchemaMetadata.String("Optional provider/source name."))),
                    new Dictionary<string, object>
                    {
                        ["clientId"] = "xcl_example",
                        ["name"] = "llama3.2:latest",
                        ["source"] = "local-ollama"
                    });
            }

            if (method == HttpMethod.POST && IsPath(path, "/v1.0/api/client-telemetry/collect", "/v1.0/api/client-telemetry/providers/collect"))
            {
                bool providerOnly = path.Contains("/providers/", StringComparison.OrdinalIgnoreCase);
                return JsonBody(
                    "Manual telemetry collection request.",
                    ObjectSchemaMetadata(
                        "Manual telemetry collection request.",
                        Array.Empty<string>(),
                        SchemaProperty("clientId", OpenApiSchemaMetadata.String("Optional client ID. Omit to collect from all connected clients.")),
                        SchemaProperty("collectHostTelemetry", BooleanSchemaMetadata("Whether host telemetry should be collected.")),
                        SchemaProperty("collectProviderTelemetry", BooleanSchemaMetadata("Whether provider telemetry should be collected.")),
                        SchemaProperty("wait", BooleanSchemaMetadata("Whether the caller wants to wait for collection completion when supported."))),
                    new Dictionary<string, object>
                    {
                        ["clientId"] = "xcl_example",
                        ["collectHostTelemetry"] = !providerOnly,
                        ["collectProviderTelemetry"] = true,
                        ["wait"] = false
                    });
            }

            if (method == HttpMethod.POST && IsPath(path, "/v1.0/api/client-telemetry/history/search"))
            {
                return JsonBody(
                    "Host telemetry search request.",
                    TelemetrySearchSchemaMetadata("Host telemetry search request."),
                    new Dictionary<string, object>
                    {
                        ["clientId"] = "xcl_example",
                        ["metric"] = "telemetry.cpu.utilizationPercent",
                        ["fromUtc"] = DateTime.UtcNow.AddHours(-1).ToString("O", CultureInfo.InvariantCulture),
                        ["toUtc"] = DateTime.UtcNow.ToString("O", CultureInfo.InvariantCulture),
                        ["page"] = 1,
                        ["pageSize"] = 50
                    });
            }

            if (method == HttpMethod.POST && IsPath(path, "/v1.0/api/client-telemetry/providers/history/search"))
            {
                return JsonBody(
                    "Provider telemetry search request.",
                    TelemetrySearchSchemaMetadata(
                        "Provider telemetry search request.",
                        SchemaProperty("providerName", OpenApiSchemaMetadata.String("Optional provider name.")),
                        SchemaProperty("modelName", OpenApiSchemaMetadata.String("Optional model name.")),
                        SchemaProperty("status", OpenApiSchemaMetadata.String("Optional provider status.")),
                        SchemaProperty("healthStatus", OpenApiSchemaMetadata.String("Optional provider health status."))),
                    new Dictionary<string, object>
                    {
                        ["clientId"] = "xcl_example",
                        ["providerName"] = "local-ollama",
                        ["metric"] = "provider.queuedRequests",
                        ["fromUtc"] = DateTime.UtcNow.AddHours(-1).ToString("O", CultureInfo.InvariantCulture),
                        ["toUtc"] = DateTime.UtcNow.ToString("O", CultureInfo.InvariantCulture),
                        ["page"] = 1,
                        ["pageSize"] = 50
                    });
            }

            if (method == HttpMethod.POST && IsPath(path, "/v1.0/api/client-telemetry/history/rollups", "/v1.0/api/client-telemetry/providers/history/rollups"))
            {
                bool providerRollup = path.Contains("/providers/", StringComparison.OrdinalIgnoreCase);
                return JsonBody(
                    "Telemetry rollup request.",
                    ObjectSchemaMetadata(
                        "Telemetry rollup request.",
                        new[] { "metric", "fromUtc", "toUtc" },
                        SchemaProperty("clientId", OpenApiSchemaMetadata.String("Optional client ID.")),
                        SchemaProperty("providerName", OpenApiSchemaMetadata.String("Optional provider name.")),
                        SchemaProperty("modelName", OpenApiSchemaMetadata.String("Optional model name.")),
                        SchemaProperty("metric", OpenApiSchemaMetadata.String("Metric name.")),
                        SchemaProperty("fromUtc", DateTimeSchema("Inclusive UTC start time.")),
                        SchemaProperty("toUtc", DateTimeSchema("Exclusive UTC end time.")),
                        SchemaProperty("bucketMinutes", IntegerSchemaMetadata("Bucket width in minutes."))),
                    new Dictionary<string, object>
                    {
                        ["metric"] = providerRollup ? "provider.queuedRequests" : "telemetry.cpu.utilizationPercent",
                        ["fromUtc"] = DateTime.UtcNow.AddHours(-1).ToString("O", CultureInfo.InvariantCulture),
                        ["toUtc"] = DateTime.UtcNow.ToString("O", CultureInfo.InvariantCulture),
                        ["bucketMinutes"] = 5
                    });
            }

            if (method == HttpMethod.POST && IsPath(path, "/v1.0/api/client-telemetry/history/delete", "/v1.0/api/client-telemetry/providers/history/delete"))
            {
                return JsonBody(
                    "Telemetry bulk delete request.",
                    ObjectSchemaMetadata(
                        "Telemetry bulk delete request.",
                        Array.Empty<string>(),
                        SchemaProperty("clientId", OpenApiSchemaMetadata.String("Optional client ID.")),
                        SchemaProperty("fromUtc", DateTimeSchema("Inclusive UTC start time.")),
                        SchemaProperty("toUtc", DateTimeSchema("Exclusive UTC end time."))),
                    new Dictionary<string, object>
                    {
                        ["clientId"] = "xcl_example",
                        ["toUtc"] = DateTime.UtcNow.AddDays(-30).ToString("O", CultureInfo.InvariantCulture)
                    });
            }

            if (method == HttpMethod.POST && IsPath(path, "/api/v1/modelleaderboard", "/v1.0/api/request-history/model-leaderboard"))
            {
                return JsonBody(
                    "Model token leaderboard request.",
                    ObjectSchemaMetadata(
                        "Model token leaderboard request.",
                        Array.Empty<string>(),
                        SchemaProperty("tenantId", OpenApiSchemaMetadata.String("Optional tenant identifier. When omitted, global model usage is returned.")),
                        SchemaProperty("fromUtc", DateTimeSchema("Inclusive UTC start time.")),
                        SchemaProperty("toUtc", DateTimeSchema("Exclusive UTC end time.")),
                        SchemaProperty("limit", IntegerSchemaMetadata("Maximum ranked models to return. Omitted or null defaults to 100 and is capped at 1000.")),
                        SchemaProperty("offset", IntegerSchemaMetadata("Number of ranked models to skip. Omitted or null defaults to 0."))),
                    new Dictionary<string, object>
                    {
                        ["fromUtc"] = DateTime.UtcNow.AddDays(-7).ToString("O", CultureInfo.InvariantCulture),
                        ["toUtc"] = DateTime.UtcNow.ToString("O", CultureInfo.InvariantCulture),
                        ["limit"] = 10,
                        ["offset"] = 0
                    });
            }

            if (method == HttpMethod.POST && IsPath(path, "/v1.0/api/ledger/entries/search"))
            {
                return JsonBody(
                    "Ledger entry search request.",
                    ObjectSchemaMetadata(
                        "Ledger entry search request.",
                        Array.Empty<string>(),
                        SchemaProperty("tenantId", OpenApiSchemaMetadata.String("Optional tenant identifier. Defaults to the caller tenant.")),
                        SchemaProperty("userId", OpenApiSchemaMetadata.String("Optional Xeno user identifier.")),
                        SchemaProperty("direction", StringEnumSchemaMetadata("Optional entry direction.", "Debit", "Credit")),
                        SchemaProperty("fromUtc", DateTimeSchema("Inclusive UTC start time.")),
                        SchemaProperty("toUtc", DateTimeSchema("Exclusive UTC end time.")),
                        SchemaProperty("requestHistoryId", OpenApiSchemaMetadata.String("Optional request history identifier.")),
                        SchemaProperty("selectedClientId", OpenApiSchemaMetadata.String("Optional serving client identifier.")),
                        SchemaProperty("continuationToken", OpenApiSchemaMetadata.String("Optional numeric continuation token.")),
                        SchemaProperty("pageSize", IntegerSchemaMetadata("Page size. Defaults to 100 and is capped at 1000."))),
                    new Dictionary<string, object>
                    {
                        ["userId"] = "usr_example",
                        ["direction"] = "Debit",
                        ["pageSize"] = 100
                    });
            }

            if (method == HttpMethod.POST && IsPath(path, "/api/v1/requesthistory/bulk", "/v1.0/api/request-history/bulk"))
            {
                return JsonBody(
                    "Bulk request-history delete request.",
                    ObjectSchemaMetadata(
                        "Bulk request-history delete request.",
                        Array.Empty<string>(),
                        SchemaProperty("ids", ArraySchemaMetadata("Request history IDs.", OpenApiSchemaMetadata.String("Request history ID."))),
                        SchemaProperty("id", OpenApiSchemaMetadata.String("Single request history ID.")),
                        SchemaProperty("model", OpenApiSchemaMetadata.String("Optional model filter.")),
                        SchemaProperty("path", OpenApiSchemaMetadata.String("Optional path filter.")),
                        SchemaProperty("selectedClientId", OpenApiSchemaMetadata.String("Optional selected client ID filter."))),
                    new Dictionary<string, object>
                    {
                        ["ids"] = new[] { "req_example" },
                        ["model"] = "llama3.2:latest",
                        ["path"] = "/v1/chat/completions"
                    });
            }

            if (method == HttpMethod.POST && IsPath(path, "/api/v1/settings", "/v1.0/api/settings"))
            {
                return JsonBody(
                    "Server settings update request. Some setting groups require a server restart before taking effect.",
                    XenoSettingsBodySchema(),
                    new Dictionary<string, object>
                    {
                        ["Rest"] = new Dictionary<string, object> { ["Hostname"] = "0.0.0.0", ["Port"] = 9000, ["Ssl"] = false },
                        ["Cors"] = new Dictionary<string, object> { ["Enabled"] = true, ["AllowedOrigins"] = new[] { "*" } },
                        ["WebSocket"] = new Dictionary<string, object> { ["Enabled"] = true, ["Path"] = "/ws/v1/clients" }
                    });
            }

            if (method == HttpMethod.POST && IsPath(path, "/api/v1/email/test", "/v1.0/api/email/test"))
            {
                return JsonBody(
                    "Administrative test email request.",
                    ObjectSchemaMetadata(
                        "Administrative test email request.",
                        new[] { "toEmail", "body" },
                        SchemaProperty("toEmail", OpenApiSchemaMetadata.String("Recipient email address.")),
                        SchemaProperty("toName", OpenApiSchemaMetadata.String("Optional recipient display name.")),
                        SchemaProperty("title", OpenApiSchemaMetadata.String("Email title. Defaults to Xeno Test Email.")),
                        SchemaProperty("subject", OpenApiSchemaMetadata.String("Email subject. Defaults to Xeno test email.")),
                        SchemaProperty("body", OpenApiSchemaMetadata.String("Plain text body rendered inside the standard Xeno email layout."))),
                    new Dictionary<string, object>
                    {
                        ["toEmail"] = "joel.christner@gmail.com",
                        ["title"] = "Xeno Test Email",
                        ["subject"] = "Xeno test email",
                        ["body"] = "hello world"
                    });
            }

            if (method == HttpMethod.POST && IsPath(path, "/api/v1/restore", "/v1.0/api/restore"))
            {
                return JsonBody(
                    "Platform-state restore request.",
                    ObjectSchemaMetadata(
                        "Platform-state restore request.",
                        Array.Empty<string>(),
                        SchemaProperty("tenants", ArraySchemaMetadata("Tenants.", OpenApiSchemaMetadata.Create("object", "Tenant."))),
                        SchemaProperty("users", ArraySchemaMetadata("Users.", OpenApiSchemaMetadata.Create("object", "User."))),
                        SchemaProperty("credentials", ArraySchemaMetadata("Credentials.", OpenApiSchemaMetadata.Create("object", "Credential.")))),
                    new Dictionary<string, object>
                    {
                        ["tenants"] = Array.Empty<object>(),
                        ["users"] = Array.Empty<object>(),
                        ["credentials"] = Array.Empty<object>()
                    });
            }

            if (method == HttpMethod.POST && IsPath(path, "/api/chat"))
            {
                return JsonBody(
                    "Ollama-compatible chat request.",
                    OllamaChatBodySchema(),
                    new Dictionary<string, object>
                    {
                        ["model"] = "llama3.2:latest",
                        ["messages"] = new[]
                        {
                            new Dictionary<string, object> { ["role"] = "user", ["content"] = "Hello! Reply in one sentence." }
                        },
                        ["stream"] = false
                    });
            }

            if (method == HttpMethod.POST && IsPath(path, "/api/generate"))
            {
                return JsonBody(
                    "Ollama-compatible generation request.",
                    OllamaGenerateBodySchema(),
                    new Dictionary<string, object>
                    {
                        ["model"] = "llama3.2:latest",
                        ["prompt"] = "Say hello in one sentence.",
                        ["stream"] = false
                    });
            }

            if (method == HttpMethod.POST && IsPath(path, "/api/embed", "/api/embeddings"))
            {
                return JsonBody(
                    "Ollama-compatible embeddings request.",
                    OllamaEmbeddingBodySchema(),
                    new Dictionary<string, object>
                    {
                        ["model"] = "llama3.2:latest",
                        ["input"] = new[] { "hello", "goodbye" }
                    });
            }

            if (method == HttpMethod.POST && path.Contains("/chat/completions", StringComparison.OrdinalIgnoreCase))
            {
                return JsonBody(
                    "OpenAI-compatible chat completion request.",
                    ChatCompletionBodySchema(),
                    new Dictionary<string, object>
                    {
                        ["model"] = "gpt-oss:120b",
                        ["messages"] = new[]
                        {
                            new Dictionary<string, object> { ["role"] = "system", ["content"] = "You are a helpful and concise assistant." },
                            new Dictionary<string, object> { ["role"] = "user", ["content"] = "Hello! Can you give me a brief explanation of how an inverted index works?" }
                        },
                        ["temperature"] = 0.7,
                        ["max_output_tokens"] = 2000,
                        ["stream"] = true,
                        ["stream_options"] = new Dictionary<string, object>
                        {
                            ["include_usage"] = true
                        }
                    });
            }

            if (method == HttpMethod.POST && path.EndsWith("/completions", StringComparison.OrdinalIgnoreCase))
            {
                return JsonBody(
                    "OpenAI-compatible text completion request.",
                    CompletionBodySchema(),
                    new Dictionary<string, object>
                    {
                        ["model"] = "gpt-oss:120b",
                        ["prompt"] = "Explain how an inverted index works in two concise paragraphs.",
                        ["temperature"] = 0.7,
                        ["max_output_tokens"] = 2000,
                        ["stream"] = true,
                        ["stream_options"] = new Dictionary<string, object>
                        {
                            ["include_usage"] = true
                        }
                    });
            }

            if (method == HttpMethod.POST && path.Contains("/embeddings", StringComparison.OrdinalIgnoreCase))
            {
                return JsonBody(
                    "OpenAI-compatible embeddings request.",
                    EmbeddingsBodySchema(),
                    new Dictionary<string, object>
                    {
                        ["model"] = "all-minilm",
                        ["input"] = new[]
                        {
                            "The quick brown fox jumps over the lazy dog.",
                            "Artificial intelligence is transforming software development.",
                            "How do I cook a perfect medium-rare steak?"
                        }
                    });
            }

            return null;
        }

        private static OpenApiRequestBodyMetadata JsonBody(string description, OpenApiSchemaMetadata schema, object example)
        {
            OpenApiRequestBodyMetadata body = OpenApiRequestBodyMetadata.Json(schema, description, true);
            if (body.Content.TryGetValue("application/json", out OpenApiMediaTypeMetadata? mediaType))
            {
                mediaType.Example = example;
            }

            return body;
        }

        private static OpenApiSchemaMetadata ObjectSchemaMetadata(string description, IEnumerable<string> required, params KeyValuePair<string, OpenApiSchemaMetadata>[] properties)
        {
            OpenApiSchemaMetadata schema = OpenApiSchemaMetadata.Create("object", description);
            schema.Properties = properties.ToDictionary(property => property.Key, property => property.Value, StringComparer.Ordinal);
            schema.Required = required.Where(item => !String.IsNullOrWhiteSpace(item)).ToList();
            return schema;
        }

        private static KeyValuePair<string, OpenApiSchemaMetadata> SchemaProperty(string name, OpenApiSchemaMetadata schema)
        {
            return new KeyValuePair<string, OpenApiSchemaMetadata>(name, schema);
        }

        private static OpenApiSchemaMetadata ArraySchemaMetadata(string description, OpenApiSchemaMetadata itemSchema)
        {
            OpenApiSchemaMetadata schema = OpenApiSchemaMetadata.CreateArray(itemSchema);
            schema.Description = description;
            return schema;
        }

        private static OpenApiSchemaMetadata BooleanSchemaMetadata(string description)
        {
            OpenApiSchemaMetadata schema = OpenApiSchemaMetadata.Boolean();
            schema.Description = description;
            return schema;
        }

        private static OpenApiSchemaMetadata IntegerSchemaMetadata(string description)
        {
            return OpenApiSchemaMetadata.Integer(description);
        }

        private static OpenApiSchemaMetadata NumberSchemaMetadata(string description)
        {
            return OpenApiSchemaMetadata.Number(description);
        }

        private static OpenApiSchemaMetadata DateTimeSchema(string description)
        {
            OpenApiSchemaMetadata schema = OpenApiSchemaMetadata.String(description);
            schema.Format = "date-time";
            return schema;
        }

        private static OpenApiSchemaMetadata StringEnumSchemaMetadata(string description, params string[] values)
        {
            OpenApiSchemaMetadata schema = OpenApiSchemaMetadata.String(description);
            schema.Enum = values.Cast<object>().ToList();
            return schema;
        }

        private static OpenApiSchemaMetadata RoleBodySchema(string description, bool requireName)
        {
            return ObjectSchemaMetadata(
                description,
                requireName ? new[] { "name" } : Array.Empty<string>(),
                SchemaProperty("name", OpenApiSchemaMetadata.String("Role name.")),
                SchemaProperty("permissionIds", ArraySchemaMetadata("Permission IDs assigned to this role.", OpenApiSchemaMetadata.String("Permission ID."))),
                SchemaProperty("active", BooleanSchemaMetadata("Whether the role is active.")));
        }

        private static OpenApiSchemaMetadata PermissionBodySchema(string description, bool requireResourceType)
        {
            return ObjectSchemaMetadata(
                description,
                requireResourceType ? new[] { "resourceType" } : Array.Empty<string>(),
                SchemaProperty("resourceType", OpenApiSchemaMetadata.String("Resource type.")),
                SchemaProperty("operationType", StringEnumSchemaMetadata("Operation type.", "Read", "Write", "Delete", "Admin")),
                SchemaProperty("permit", BooleanSchemaMetadata("Whether this permission permits the operation.")),
                SchemaProperty("active", BooleanSchemaMetadata("Whether the permission is active.")));
        }

        private static OpenApiSchemaMetadata RoutingPolicyBodySchema(string description, bool requireName)
        {
            return ObjectSchemaMetadata(
                description,
                requireName ? new[] { "name" } : Array.Empty<string>(),
                SchemaProperty("tenantId", OpenApiSchemaMetadata.String("Tenant identifier.")),
                SchemaProperty("name", OpenApiSchemaMetadata.String("Policy name.")),
                SchemaProperty("modelName", OpenApiSchemaMetadata.String("Model match. Use '*' to match all models.")),
                SchemaProperty("clientName", OpenApiSchemaMetadata.String("Client name match. Use '*' to match all clients.")),
                SchemaProperty("requestTimeoutMs", IntegerSchemaMetadata("Request timeout in milliseconds.")),
                SchemaProperty("loadBalancingMode", StringEnumSchemaMetadata("Load-balancing mode.", "RoundRobin", "Random", "FirstAvailable", "LeastRecentlyUsed", "Adaptive")),
                SchemaProperty("adaptive", OpenApiSchemaMetadata.Create("object", "Adaptive load-balancing weights and sample count.")),
                SchemaProperty("active", BooleanSchemaMetadata("Whether the routing policy is active.")));
        }

        private static Dictionary<string, object> RoutingPolicyExample()
        {
            return new Dictionary<string, object>
            {
                ["name"] = "GPU clients for gpt-oss",
                ["modelName"] = "gpt-oss:120b",
                ["clientName"] = "*",
                ["requestTimeoutMs"] = 300000,
                ["loadBalancingMode"] = "LeastRecentlyUsed",
                ["adaptive"] = new Dictionary<string, object>
                {
                    ["sampleCount"] = 3,
                    ["capacityWeight"] = 40,
                    ["inFlightWeight"] = 30,
                    ["clientWeight"] = 20,
                    ["heartbeatWeight"] = 10
                },
                ["active"] = true
            };
        }

        private static OpenApiSchemaMetadata TelemetrySearchSchemaMetadata(string description, params KeyValuePair<string, OpenApiSchemaMetadata>[] additionalProperties)
        {
            List<KeyValuePair<string, OpenApiSchemaMetadata>> properties = new List<KeyValuePair<string, OpenApiSchemaMetadata>>
            {
                SchemaProperty("clientId", OpenApiSchemaMetadata.String("Optional client ID.")),
                SchemaProperty("clientName", OpenApiSchemaMetadata.String("Optional client name.")),
                SchemaProperty("machineName", OpenApiSchemaMetadata.String("Optional machine name.")),
                SchemaProperty("metric", OpenApiSchemaMetadata.String("Optional metric name.")),
                SchemaProperty("metricMinimum", NumberSchemaMetadata("Optional minimum metric value.")),
                SchemaProperty("metricMaximum", NumberSchemaMetadata("Optional maximum metric value.")),
                SchemaProperty("fromUtc", DateTimeSchema("Inclusive UTC start time.")),
                SchemaProperty("toUtc", DateTimeSchema("Exclusive UTC end time.")),
                SchemaProperty("page", IntegerSchemaMetadata("Page number.")),
                SchemaProperty("pageSize", IntegerSchemaMetadata("Page size.")),
                SchemaProperty("sortDescending", BooleanSchemaMetadata("Whether newest samples are returned first."))
            };
            properties.AddRange(additionalProperties);
            return ObjectSchemaMetadata(description, Array.Empty<string>(), properties.ToArray());
        }

        private static OpenApiSchemaMetadata ChatCompletionBodySchema()
        {
            OpenApiSchemaMetadata messageSchema = ObjectSchemaMetadata(
                "Chat message.",
                new[] { "role", "content" },
                SchemaProperty("role", StringEnumSchemaMetadata("Message role.", "system", "user", "assistant", "tool")),
                SchemaProperty("content", OpenApiSchemaMetadata.String("Message content.")));

            return ObjectSchemaMetadata(
                "Chat completion request.",
                new[] { "model", "messages" },
                SchemaProperty("model", OpenApiSchemaMetadata.String("Model name.")),
                SchemaProperty("messages", ArraySchemaMetadata("Chat messages.", messageSchema)),
                SchemaProperty("temperature", NumberSchemaMetadata("Sampling temperature.")),
                SchemaProperty("max_tokens", IntegerSchemaMetadata("Maximum output tokens.")),
                SchemaProperty("max_output_tokens", IntegerSchemaMetadata("Maximum output tokens.")),
                SchemaProperty("stream", BooleanSchemaMetadata("Whether to stream the response.")),
                SchemaProperty("stream_options", StreamOptionsSchemaMetadata()));
        }

        private static OpenApiSchemaMetadata CompletionBodySchema()
        {
            return ObjectSchemaMetadata(
                "Text completion request.",
                new[] { "model", "prompt" },
                SchemaProperty("model", OpenApiSchemaMetadata.String("Model name.")),
                SchemaProperty("prompt", OpenApiSchemaMetadata.String("Prompt text.")),
                SchemaProperty("temperature", NumberSchemaMetadata("Sampling temperature.")),
                SchemaProperty("max_tokens", IntegerSchemaMetadata("Maximum output tokens.")),
                SchemaProperty("max_output_tokens", IntegerSchemaMetadata("Maximum output tokens.")),
                SchemaProperty("stream", BooleanSchemaMetadata("Whether to stream the response.")),
                SchemaProperty("stream_options", StreamOptionsSchemaMetadata()));
        }

        private static OpenApiSchemaMetadata StreamOptionsSchemaMetadata()
        {
            return ObjectSchemaMetadata(
                "Streaming options.",
                Array.Empty<string>(),
                SchemaProperty("include_usage", BooleanSchemaMetadata("Whether streamed responses should include provider usage telemetry.")));
        }

        private static OpenApiSchemaMetadata EmbeddingsBodySchema()
        {
            return ObjectSchemaMetadata(
                "Embeddings request.",
                new[] { "model", "input" },
                SchemaProperty("model", OpenApiSchemaMetadata.String("Embedding model name.")),
                SchemaProperty("input", ArraySchemaMetadata("Input strings.", OpenApiSchemaMetadata.String("Input text."))));
        }

        private static OpenApiSchemaMetadata OllamaChatBodySchema()
        {
            OpenApiSchemaMetadata messageSchema = ObjectSchemaMetadata(
                "Ollama chat message.",
                new[] { "role", "content" },
                SchemaProperty("role", StringEnumSchemaMetadata("Message role.", "system", "user", "assistant", "tool")),
                SchemaProperty("content", OpenApiSchemaMetadata.String("Message content.")));

            return ObjectSchemaMetadata(
                "Ollama chat request.",
                new[] { "model", "messages" },
                SchemaProperty("model", OpenApiSchemaMetadata.String("Model name.")),
                SchemaProperty("messages", ArraySchemaMetadata("Chat messages.", messageSchema)),
                SchemaProperty("stream", BooleanSchemaMetadata("Whether to stream the response.")));
        }

        private static OpenApiSchemaMetadata OllamaGenerateBodySchema()
        {
            return ObjectSchemaMetadata(
                "Ollama generation request.",
                new[] { "model", "prompt" },
                SchemaProperty("model", OpenApiSchemaMetadata.String("Model name.")),
                SchemaProperty("prompt", OpenApiSchemaMetadata.String("Prompt text.")),
                SchemaProperty("stream", BooleanSchemaMetadata("Whether to stream the response.")));
        }

        private static OpenApiSchemaMetadata OllamaEmbeddingBodySchema()
        {
            return ObjectSchemaMetadata(
                "Ollama embeddings request.",
                new[] { "model" },
                SchemaProperty("model", OpenApiSchemaMetadata.String("Embedding model name.")),
                SchemaProperty("input", ArraySchemaMetadata("Input strings.", OpenApiSchemaMetadata.String("Input text."))),
                SchemaProperty("prompt", OpenApiSchemaMetadata.String("Legacy single prompt.")));
        }

        private static OpenApiSchemaMetadata XenoSettingsBodySchema()
        {
            return ObjectSchemaMetadata(
                "Server settings.",
                Array.Empty<string>(),
                SchemaProperty("Rest", OpenApiSchemaMetadata.Create("object", "REST settings.")),
                SchemaProperty("Cors", OpenApiSchemaMetadata.Create("object", "CORS settings.")),
                SchemaProperty("Logging", OpenApiSchemaMetadata.Create("object", "Logging settings.")),
                SchemaProperty("WebSocket", OpenApiSchemaMetadata.Create("object", "WebSocket settings.")),
                SchemaProperty("Database", OpenApiSchemaMetadata.Create("object", "Database settings.")),
                SchemaProperty("Auth", OpenApiSchemaMetadata.Create("object", "Authentication settings.")),
                SchemaProperty("RequestHistory", OpenApiSchemaMetadata.Create("object", "Request history settings.")),
                SchemaProperty("Routing", OpenApiSchemaMetadata.Create("object", "Routing settings.")),
                SchemaProperty("TelemetryCollection", OpenApiSchemaMetadata.Create("object", "Server telemetry collection settings.")),
                SchemaProperty("Ledger", OpenApiSchemaMetadata.Create("object", "Ledger integration settings.")),
                SchemaProperty("Email", OpenApiSchemaMetadata.Create("object", "Email notification settings.")));
        }

        private async Task PreflightRouteAsync(HttpContextBase context)
        {
            ApplyCors(context);
            context.Response.StatusCode = 204;
            await context.Response.Send(context.Token).ConfigureAwait(false);
        }

        private async Task PostRoutingRouteAsync(HttpContextBase context)
        {
            ApplyCors(context);
            await Task.CompletedTask.ConfigureAwait(false);
        }

        private async Task OpenApiRouteAsync(HttpContextBase context)
        {
            ApplyCors(context);
            await SendJsonAsync(context, BuildOpenApi()).ConfigureAwait(false);
        }

        private async Task SwaggerRouteAsync(HttpContextBase context)
        {
            ApplyCors(context);
            context.Response.ContentType = "text/html";
            await context.Response.Send(SwaggerHtml(), context.Token).ConfigureAwait(false);
        }

        private async Task ExceptionRouteAsync(HttpContextBase context, Exception exception)
        {
            ApplyCors(context);
            context.Response.StatusCode = 500;
            await SendJsonAsync(context, new { error = "InternalServerError", message = exception.Message }).ConfigureAwait(false);
        }

        private async Task DefaultRouteAsync(HttpContextBase context)
        {
            string path = context.Request.Url.RawWithoutQuery ?? "/";
            string method = context.Request.Method.ToString().ToUpperInvariant();

            try
            {
                ApplyCors(context);
                await HandleRestAsync(context, method, path, context.Token).ConfigureAwait(false);
            }
            catch (AuthorizationDeniedException ex)
            {
                Interlocked.Increment(ref _AuthorizationDenialCount);
                if (_Database != null) await SavePlatformAsync(context.Token).ConfigureAwait(false);
                context.Response.StatusCode = ex.StatusCode;
                await SendJsonAsync(context, new { error = ex.ReasonCode, message = ex.Message }).ConfigureAwait(false);
            }
            catch (UnauthorizedAccessException ex)
            {
                Interlocked.Increment(ref _AuthFailureCount);
                context.Response.StatusCode = 401;
                await SendJsonAsync(context, new { error = "Unauthorized", message = ex.Message }).ConfigureAwait(false);
            }
            catch (HttpStatusException ex)
            {
                context.Response.StatusCode = ex.StatusCode;
                await SendJsonAsync(context, new { error = ex.ReasonCode, message = ex.Message }).ConfigureAwait(false);
            }
            catch (InvalidDataException ex)
            {
                context.Response.StatusCode = 400;
                await SendJsonAsync(context, new { error = "BadRequest", message = ex.Message }).ConfigureAwait(false);
            }
            catch (ArgumentException ex)
            {
                context.Response.StatusCode = 400;
                await SendJsonAsync(context, new { error = "BadRequest", message = ex.Message }).ConfigureAwait(false);
            }
            catch (KeyNotFoundException ex)
            {
                context.Response.StatusCode = 404;
                await SendJsonAsync(context, new { error = "NotFound", message = ex.Message }).ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
                context.Response.StatusCode = 499;
                await SendJsonAsync(context, new { error = "ClientClosedRequest", message = "Request was cancelled." }).ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                context.Response.StatusCode = 500;
                await SendJsonAsync(context, new { error = "InternalServerError", message = ex.Message }).ConfigureAwait(false);
            }
        }

        private async Task HandleRestAsync(HttpContextBase context, string method, string path, CancellationToken token)
        {
            token.ThrowIfCancellationRequested();

            if (method == "GET" && IsPath(path, "/", "/health", "/v1.0/api/health"))
            {
                await SendJsonAsync(context, new { status = "healthy", product = Constants.ProductName, version = Constants.Version }).ConfigureAwait(false);
                return;
            }

            if (method == "GET" && IsPath(path, "/ping", "/api/v1/ping", "/v1.0/api/ping"))
            {
                string sequenceValue = DecodeQueryValue(context.Request.Query.Elements.Get("sequence"));
                string clientSentUtc = DecodeQueryValue(context.Request.Query.Elements.Get("clientSentUtc"));
                string? echoedClientSentUtc = String.IsNullOrWhiteSpace(clientSentUtc) ? null : clientSentUtc;
                int sequence = 0;
                Int32.TryParse(sequenceValue, NumberStyles.Integer, CultureInfo.InvariantCulture, out sequence);
                await SendJsonAsync(context, new
                {
                    status = "healthy",
                    product = Constants.ProductName,
                    version = Constants.Version,
                    sequence,
                    clientSentUtc = echoedClientSentUtc,
                    serverUtc = DateTime.UtcNow
                }).ConfigureAwait(false);
                return;
            }

            if (method == "GET" && path.Equals("/metrics", StringComparison.OrdinalIgnoreCase))
            {
                await SendMetricsAsync(context).ConfigureAwait(false);
                return;
            }

            if (method == "GET" && path.Equals("/swagger", StringComparison.OrdinalIgnoreCase))
            {
                context.Response.ContentType = "text/html";
                await context.Response.Send(SwaggerHtml(), context.Token).ConfigureAwait(false);
                return;
            }

            if (method == "GET" && path.Equals("/openapi.json", StringComparison.OrdinalIgnoreCase))
            {
                await SendJsonAsync(context, BuildOpenApi()).ConfigureAwait(false);
                return;
            }

            if (method == "POST" && IsPath(path, "/api/v1/auth/login", "/v1.0/api/auth/login"))
            {
                if (await RejectRateLimitedAuthAttemptAsync(context).ConfigureAwait(false)) return;
                await HandleLoginAsync(context, token).ConfigureAwait(false);
                return;
            }

            if ((method == "GET" || method == "POST") && IsPath(path, "/api/v1/auth/tenants", "/v1.0/api/auth/tenants"))
            {
                await HandleLoginTenantLookupAsync(context).ConfigureAwait(false);
                return;
            }

            if (method == "POST" && IsPath(path, "/api/v1/auth/dashboard/login", "/v1.0/api/auth/dashboard/login"))
            {
                if (await RejectRateLimitedAuthAttemptAsync(context).ConfigureAwait(false)) return;
                await HandleDashboardLoginAsync(context, token).ConfigureAwait(false);
                return;
            }

            if (method == "POST" && IsPath(path, "/api/v1/onboarding/signup", "/v1.0/api/onboarding/signup"))
            {
                if (await RejectRateLimitedAuthAttemptAsync(context).ConfigureAwait(false)) return;
                await HandleOnboardingSignupAsync(context, token).ConfigureAwait(false);
                return;
            }

            if ((method == "GET" || method == "POST") && IsPath(path, "/api/v1/modelleaderboard", "/v1.0/api/request-history/model-leaderboard"))
            {
                ModelTokenLeaderboardRequest body = method == "POST"
                    ? NormalizeModelTokenLeaderboardRequest(ReadJsonBody<ModelTokenLeaderboardRequest>(context))
                    : BuildModelTokenLeaderboardRequest(context);
                ModelTokenLeaderboardResponse result = _Database == null
                    ? new ModelTokenLeaderboardResponse { Limit = body.Limit ?? 100, Offset = body.Offset ?? 0 }
                    : await _Database.GetModelTokenLeaderboardAsync(body, token).ConfigureAwait(false);
                await SendJsonAsync(context, result).ConfigureAwait(false);
                return;
            }

            RequestContext requestContext = CreateRequestContext(context);

            if (method == "POST" && IsPath(path, "/api/v1/auth/token", "/v1.0/api/auth/token"))
            {
                if (await RejectRateLimitedAuthAttemptAsync(context).ConfigureAwait(false)) return;
                await HandleCreateTokenAsync(context, requestContext, token).ConfigureAwait(false);
                return;
            }

            if (method == "GET" && IsPath(path, "/api/v1/auth/token/details", "/v1.0/api/auth/token/details"))
            {
                await RequirePermissionAsync(requestContext, "Authentication", OperationTypeEnum.Read, token).ConfigureAwait(false);
                AuthSession? session = String.IsNullOrWhiteSpace(requestContext.SessionId)
                    ? null
                    : RequirePlatform().AuthSessions.FirstOrDefault(item => item.Id == requestContext.SessionId && item.TenantId == requestContext.TenantId);
                await SendJsonAsync(context, new
                {
                    requestContext.IsAuthenticated,
                    requestContext.TenantId,
                    requestContext.UserId,
                    requestContext.CredentialId,
                    requestContext.SessionId,
                    requestContext.PrincipalType,
                    requestContext.AuthScheme,
                    expiresUtc = session?.ExpiresUtc
                }).ConfigureAwait(false);
                return;
            }

            if (method == "POST" && IsPath(path, "/api/v1/auth/token/refresh", "/v1.0/api/auth/token/refresh"))
            {
                await RequirePermissionAsync(requestContext, "Authentication", OperationTypeEnum.Read, token).ConfigureAwait(false);
                AuthTokenResult result = _Authentication.IssueToken(
                    RequirePlatform(),
                    _Settings.Auth,
                    requestContext.TenantId,
                    requestContext.PrincipalType == PrincipalTypeEnum.None ? PrincipalTypeEnum.User : requestContext.PrincipalType,
                    requestContext.UserId,
                    requestContext.CredentialId,
                    AuthSchemeEnum.Bearer);
                if (!String.IsNullOrWhiteSpace(requestContext.SessionId)) _Authentication.RevokeSession(RequirePlatform(), requestContext.SessionId);
                await SavePlatformAsync(token).ConfigureAwait(false);
                await SendJsonAsync(context, result).ConfigureAwait(false);
                return;
            }

            if (method == "POST" && IsPath(path, "/api/v1/auth/logout", "/v1.0/api/auth/logout"))
            {
                await RequirePermissionAsync(requestContext, "Authentication", OperationTypeEnum.Read, token).ConfigureAwait(false);
                bool revoked = _Authentication.RevokeSession(RequirePlatform(), requestContext.SessionId);
                await SavePlatformAsync(token).ConfigureAwait(false);
                await SendJsonAsync(context, new { revoked }).ConfigureAwait(false);
                return;
            }

            if (method == "POST" && IsPath(path, "/api/v1/admin/nuke/tenant", "/v1.0/api/admin/nuke/tenant"))
            {
                AdminNukeResponse result = await HandleNukeTenantAsync(context, requestContext, token).ConfigureAwait(false);
                await SendJsonAsync(context, result).ConfigureAwait(false);
                return;
            }

            if (method == "POST" && IsPath(path, "/api/v1/admin/nuke/user", "/v1.0/api/admin/nuke/user"))
            {
                AdminNukeResponse result = await HandleNukeUserAsync(context, requestContext, token).ConfigureAwait(false);
                await SendJsonAsync(context, result).ConfigureAwait(false);
                return;
            }

            if (method == "GET" && IsPath(path, "/api/v1/tenants", "/v1.0/api/tenants"))
            {
                await RequirePermissionAsync(requestContext, "Tenants", OperationTypeEnum.Read, token).ConfigureAwait(false);
                List<Tenant> visibleTenants = RequirePlatform().Tenants.Where(item => IsTenantVisible(requestContext, item.Id)).ToList();
                await SendJsonAsync(context, visibleTenants).ConfigureAwait(false);
                return;
            }

            if (method == "POST" && IsPath(path, "/api/v1/tenants", "/v1.0/api/tenants"))
            {
                await RequirePermissionAsync(requestContext, "Admin", OperationTypeEnum.Admin, token).ConfigureAwait(false);
                TenantSaveRequest tenantBody = ReadJsonBody<TenantSaveRequest>(context);
                Tenant tenant = CreateTenant(tenantBody);
                RequirePlatform().Tenants.Add(tenant);
                RequirePlatform().EnsureTenantDefaults(tenant.Id);
                bool allowDeferred = tenantBody.DeferSubordinateProvisioning == true;
                await RequireTenantLifecycle().CreateAsync(tenant, ListSeedUsers(tenant.Id), allowDeferred, requestContext, token).ConfigureAwait(false);
                await SavePlatformAsync(token).ConfigureAwait(false);
                await SendJsonAsync(context, tenant).ConfigureAwait(false);
                return;
            }

            if (method == "PUT" && TryGetPathId(path, "/api/v1/tenants/", "/v1.0/api/tenants/") is string updateTenantId)
            {
                await RequirePermissionAsync(requestContext, "Admin", OperationTypeEnum.Admin, token).ConfigureAwait(false);
                Tenant? originalTenant = RequirePlatform().Tenants.FirstOrDefault(item => item.Id == updateTenantId);
                bool wasActive = originalTenant?.Active ?? false;
                TenantSaveRequest tenantBody = ReadJsonBody<TenantSaveRequest>(context);
                Tenant tenant = UpdateTenant(updateTenantId, tenantBody);
                if (wasActive && !tenant.Active)
                {
                    await RequireTenantLifecycle().SuspendAsync(tenant, requestContext, token).ConfigureAwait(false);
                }
                else if (!wasActive && tenant.Active)
                {
                    await RequireTenantLifecycle().ReactivateAsync(tenant, requestContext, token).ConfigureAwait(false);
                    await RequireTenantLifecycle().ReconcileAsync(tenant, requestContext, token).ConfigureAwait(false);
                }
                else
                {
                    await RequireTenantLifecycle().UpdateAsync(tenant, requestContext, token).ConfigureAwait(false);
                }
                await SavePlatformAsync(token).ConfigureAwait(false);
                await SendJsonAsync(context, tenant).ConfigureAwait(false);
                return;
            }

            if (method == "DELETE" && TryGetPathId(path, "/api/v1/tenants/", "/v1.0/api/tenants/") is string deleteTenantId)
            {
                await RequirePermissionAsync(requestContext, "Admin", OperationTypeEnum.Admin, token).ConfigureAwait(false);
                await DeleteTenantAsync(deleteTenantId, requestContext, token).ConfigureAwait(false);
                await SendNoContentAsync(context).ConfigureAwait(false);
                return;
            }

            if (method == "GET" && IsPath(path, "/api/v1/users", "/v1.0/api/users"))
            {
                await RequirePermissionAsync(requestContext, "Users", OperationTypeEnum.Read, token).ConfigureAwait(false);
                await SendJsonAsync(context, ListVisibleUsers(requestContext).Select(RedactUser).ToList()).ConfigureAwait(false);
                return;
            }

            if (method == "POST" && IsPath(path, "/api/v1/users", "/v1.0/api/users"))
            {
                await RequirePermissionAsync(requestContext, "Users", OperationTypeEnum.Write, token).ConfigureAwait(false);
                User user = CreateUser(requestContext, context);
                RequirePlatform().Users.Add(user);
                await ProvisionLedgerAccountForUserAsync(user, requestContext, token).ConfigureAwait(false);
                await SavePlatformAsync(token).ConfigureAwait(false);
                await SendJsonAsync(context, RedactUser(user)).ConfigureAwait(false);
                return;
            }

            if (method == "PUT" && TryGetPathId(path, "/api/v1/users/", "/v1.0/api/users/") is string updateUserId)
            {
                await RequireUserUpdatePermissionAsync(requestContext, updateUserId, token).ConfigureAwait(false);
                User user = UpdateUser(requestContext, updateUserId, context);
                await ProvisionLedgerAccountForUserAsync(user, requestContext, token).ConfigureAwait(false);
                await SavePlatformAsync(token).ConfigureAwait(false);
                await SendJsonAsync(context, RedactUser(user)).ConfigureAwait(false);
                return;
            }

            if (method == "DELETE" && TryGetPathId(path, "/api/v1/users/", "/v1.0/api/users/") is string deleteUserId)
            {
                await RequirePermissionAsync(requestContext, "Users", OperationTypeEnum.Delete, token).ConfigureAwait(false);
                int removed = DeleteUser(requestContext, deleteUserId);
                if (removed > 0) await SavePlatformAsync(token).ConfigureAwait(false);
                await SendNoContentAsync(context).ConfigureAwait(false);
                return;
            }

            if (method == "GET" && IsPath(path, "/api/v1/credentials", "/v1.0/api/credentials"))
            {
                await RequirePermissionAsync(requestContext, "Credentials", OperationTypeEnum.Read, token).ConfigureAwait(false);
                await SendJsonAsync(context, ListVisibleCredentials(requestContext).Select(RedactCredential).ToList()).ConfigureAwait(false);
                return;
            }

            if (method == "POST" && IsPath(path, "/api/v1/credentials", "/v1.0/api/credentials"))
            {
                await RequirePermissionAsync(requestContext, "Credentials", OperationTypeEnum.Write, token).ConfigureAwait(false);
                Credential credential = CreateCredential(requestContext, context);
                RequirePlatform().Credentials.Add(credential);
                await SavePlatformAsync(token).ConfigureAwait(false);
                await SendJsonAsync(context, new
                {
                    credential.Id,
                    credential.TenantId,
                    credential.UserId,
                    credential.AccessKey,
                    credential.Active
                }).ConfigureAwait(false);
                return;
            }

            if (method == "PUT" && TryGetPathId(path, "/api/v1/credentials/", "/v1.0/api/credentials/") is string updateCredentialId)
            {
                await RequirePermissionAsync(requestContext, "Credentials", OperationTypeEnum.Write, token).ConfigureAwait(false);
                Credential credential = UpdateCredential(requestContext, updateCredentialId, context);
                await SavePlatformAsync(token).ConfigureAwait(false);
                await SendJsonAsync(context, RedactCredential(credential)).ConfigureAwait(false);
                return;
            }

            if (method == "DELETE" && TryGetPathId(path, "/api/v1/credentials/", "/v1.0/api/credentials/") is string deleteCredentialId)
            {
                await RequirePermissionAsync(requestContext, "Credentials", OperationTypeEnum.Delete, token).ConfigureAwait(false);
                int removed = DeleteCredential(requestContext, deleteCredentialId);
                if (removed > 0) await SavePlatformAsync(token).ConfigureAwait(false);
                await SendNoContentAsync(context).ConfigureAwait(false);
                return;
            }

            if (method == "GET" && IsPath(path, "/api/v1/roles", "/v1.0/api/roles"))
            {
                await RequirePermissionAsync(requestContext, "Roles", OperationTypeEnum.Read, token).ConfigureAwait(false);
                await SendJsonAsync(context, RequirePlatform().Roles.Where(item => item.TenantId == requestContext.TenantId).ToList()).ConfigureAwait(false);
                return;
            }

            if (method == "POST" && IsPath(path, "/api/v1/roles", "/v1.0/api/roles"))
            {
                await RequirePermissionAsync(requestContext, "Admin", OperationTypeEnum.Admin, token).ConfigureAwait(false);
                Role role = CreateRole(requestContext, context);
                RequirePlatform().Roles.Add(role);
                await SavePlatformAsync(token).ConfigureAwait(false);
                await SendJsonAsync(context, role).ConfigureAwait(false);
                return;
            }

            if (method == "PUT" && TryGetPathId(path, "/api/v1/roles/", "/v1.0/api/roles/") is string updateRoleId)
            {
                await RequirePermissionAsync(requestContext, "Admin", OperationTypeEnum.Admin, token).ConfigureAwait(false);
                Role role = UpdateRole(requestContext, updateRoleId, context);
                await SavePlatformAsync(token).ConfigureAwait(false);
                await SendJsonAsync(context, role).ConfigureAwait(false);
                return;
            }

            if (method == "DELETE" && TryGetPathId(path, "/api/v1/roles/", "/v1.0/api/roles/") is string deleteRoleId)
            {
                await RequirePermissionAsync(requestContext, "Admin", OperationTypeEnum.Admin, token).ConfigureAwait(false);
                int removed = DeleteRole(requestContext, deleteRoleId);
                if (removed > 0) await SavePlatformAsync(token).ConfigureAwait(false);
                await SendNoContentAsync(context).ConfigureAwait(false);
                return;
            }

            if (method == "GET" && IsPath(path, "/api/v1/permissions", "/v1.0/api/permissions"))
            {
                await RequirePermissionAsync(requestContext, "Permissions", OperationTypeEnum.Read, token).ConfigureAwait(false);
                await SendJsonAsync(context, RequirePlatform().Permissions.Where(item => item.TenantId == requestContext.TenantId).ToList()).ConfigureAwait(false);
                return;
            }

            if (method == "GET" && IsPath(path, "/api/v1/permissions/grantable", "/v1.0/api/permissions/grantable"))
            {
                await RequirePermissionAsync(requestContext, "Admin", OperationTypeEnum.Admin, token).ConfigureAwait(false);
                await SendJsonAsync(context, ListGrantablePermissions(requestContext)).ConfigureAwait(false);
                return;
            }

            if (method == "POST" && IsPath(path, "/api/v1/permissions", "/v1.0/api/permissions"))
            {
                await RequirePermissionAsync(requestContext, "Admin", OperationTypeEnum.Admin, token).ConfigureAwait(false);
                Permission permission = CreatePermission(requestContext, context);
                RequirePlatform().Permissions.Add(permission);
                await SavePlatformAsync(token).ConfigureAwait(false);
                await SendJsonAsync(context, permission).ConfigureAwait(false);
                return;
            }

            if (method == "PUT" && TryGetPathId(path, "/api/v1/permissions/", "/v1.0/api/permissions/") is string updatePermissionId)
            {
                await RequirePermissionAsync(requestContext, "Admin", OperationTypeEnum.Admin, token).ConfigureAwait(false);
                Permission permission = UpdatePermission(requestContext, updatePermissionId, context);
                await SavePlatformAsync(token).ConfigureAwait(false);
                await SendJsonAsync(context, permission).ConfigureAwait(false);
                return;
            }

            if (method == "DELETE" && TryGetPathId(path, "/api/v1/permissions/", "/v1.0/api/permissions/") is string deletePermissionId)
            {
                await RequirePermissionAsync(requestContext, "Admin", OperationTypeEnum.Admin, token).ConfigureAwait(false);
                int removed = DeletePermission(requestContext, deletePermissionId);
                if (removed > 0) await SavePlatformAsync(token).ConfigureAwait(false);
                await SendNoContentAsync(context).ConfigureAwait(false);
                return;
            }

            if (method == "GET" && IsPath(path, "/api/v1/roleassignments", "/v1.0/api/role-assignments"))
            {
                await RequirePermissionAsync(requestContext, "Admin", OperationTypeEnum.Admin, token).ConfigureAwait(false);
                await SendJsonAsync(context, RequirePlatform().RoleAssignments.Where(item => IsTenantVisible(requestContext, item.TenantId)).ToList()).ConfigureAwait(false);
                return;
            }

            if (method == "POST" && IsPath(path, "/api/v1/roleassignments", "/v1.0/api/role-assignments"))
            {
                await RequirePermissionAsync(requestContext, "Admin", OperationTypeEnum.Admin, token).ConfigureAwait(false);
                RoleAssignment assignment = CreateRoleAssignment(requestContext, context);
                RequirePlatform().RoleAssignments.Add(assignment);
                await SavePlatformAsync(token).ConfigureAwait(false);
                await SendJsonAsync(context, assignment).ConfigureAwait(false);
                return;
            }

            if (method == "DELETE" && TryGetPathId(path, "/api/v1/roleassignments/", "/v1.0/api/role-assignments/") is string deleteRoleAssignmentId)
            {
                await RequirePermissionAsync(requestContext, "Admin", OperationTypeEnum.Admin, token).ConfigureAwait(false);
                int removed = DeleteRoleAssignment(requestContext, deleteRoleAssignmentId);
                if (removed > 0) await SavePlatformAsync(token).ConfigureAwait(false);
                await SendNoContentAsync(context).ConfigureAwait(false);
                return;
            }

            if (method == "GET" && IsPath(path, "/api/v1/effective-permissions", "/v1.0/api/effective-permissions"))
            {
                await RequirePermissionAsync(requestContext, "Authentication", OperationTypeEnum.Read, token).ConfigureAwait(false);
                await SendJsonAsync(context, _Authentication.GetEffectivePermissions(RequirePlatform(), requestContext)).ConfigureAwait(false);
                return;
            }

            if (method == "GET" && IsPath(path, "/api/v1/audit", "/v1.0/api/audit"))
            {
                await RequirePermissionAsync(requestContext, "Admin", OperationTypeEnum.Admin, token).ConfigureAwait(false);
                await SendJsonAsync(context, RequirePlatform().AuditRecords.Where(item => item.TenantId == requestContext.TenantId).OrderByDescending(item => item.CreatedUtc).ToList()).ConfigureAwait(false);
                return;
            }

            if (method == "DELETE" && TryGetPathId(path, "/api/v1/audit/", "/v1.0/api/audit/") is string deleteAuditId)
            {
                await RequirePermissionAsync(requestContext, "Admin", OperationTypeEnum.Admin, token).ConfigureAwait(false);
                int removed = RequirePlatform().AuditRecords.RemoveAll(item => item.Id == deleteAuditId && item.TenantId == requestContext.TenantId);
                if (removed > 0) await SavePlatformAsync(token).ConfigureAwait(false);
                await SendNoContentAsync(context).ConfigureAwait(false);
                return;
            }

            if (method == "GET" && IsPath(path, "/api/v1/clients", "/v1.0/api/clients"))
            {
                await RequirePermissionAsync(requestContext, "Clients", OperationTypeEnum.Read, token).ConfigureAwait(false);
                await SendJsonAsync(context, RequirePlatform().Clients.Where(item => item.TenantId == requestContext.TenantId).ToList()).ConfigureAwait(false);
                return;
            }

            if (method == "DELETE" && TryGetPathId(path, "/api/v1/clients/", "/v1.0/api/clients/") is string deleteClientId)
            {
                await RequirePermissionAsync(requestContext, "Clients", OperationTypeEnum.Delete, token).ConfigureAwait(false);
                int removed = DeleteClient(requestContext, deleteClientId);
                if (removed > 0) await SavePlatformAsync(token).ConfigureAwait(false);
                await SendNoContentAsync(context).ConfigureAwait(false);
                return;
            }

            if (method == "DELETE" && IsPath(path, "/api/v1/clientmodels", "/v1.0/api/client-models"))
            {
                await RequirePermissionAsync(requestContext, "Clients", OperationTypeEnum.Delete, token).ConfigureAwait(false);
                int removed = DeleteClientModel(requestContext, context);
                if (removed > 0) await SavePlatformAsync(token).ConfigureAwait(false);
                await SendJsonAsync(context, new { removed }).ConfigureAwait(false);
                return;
            }

            if (method == "GET" && IsPath(path, "/v1.0/api/client-telemetry/status"))
            {
                await RequirePermissionAsync(requestContext, "ClientTelemetry", OperationTypeEnum.Read, token).ConfigureAwait(false);
                await SendJsonAsync(context, BuildTelemetryStatus(requestContext)).ConfigureAwait(false);
                return;
            }

            if (method == "GET" && IsPath(path, "/v1.0/api/client-telemetry/latest"))
            {
                await RequirePermissionAsync(requestContext, "ClientTelemetry", OperationTypeEnum.Read, token).ConfigureAwait(false);
                string? clientId = context.Request.Query.Elements.Get("clientId");
                ServerClientTelemetryLatestResponse latest = new ServerClientTelemetryLatestResponse();
                latest.HostTelemetry.AddRange(_TelemetryLatestCache.GetHost(requestContext.TenantId, clientId));
                latest.ProviderTelemetry.AddRange(_TelemetryLatestCache.GetProviders(requestContext.TenantId, clientId));
                if (latest.HostTelemetry.Count == 0 && _Database?.ClientTelemetry != null)
                {
                    latest.HostTelemetry.AddRange(await _Database.ClientTelemetry.LatestHostByClientAsync(requestContext.TenantId, clientId, token).ConfigureAwait(false));
                }

                if (latest.ProviderTelemetry.Count == 0 && _Database?.ClientProviderTelemetry != null)
                {
                    latest.ProviderTelemetry.AddRange(await _Database.ClientProviderTelemetry.LatestProviderByClientAsync(requestContext.TenantId, clientId, token).ConfigureAwait(false));
                }

                await SendJsonAsync(context, latest).ConfigureAwait(false);
                return;
            }

            if (method == "GET" && IsPath(path, "/v1.0/api/client-telemetry/providers/latest"))
            {
                await RequirePermissionAsync(requestContext, "ClientTelemetry", OperationTypeEnum.Read, token).ConfigureAwait(false);
                string? clientId = context.Request.Query.Elements.Get("clientId");
                IReadOnlyList<ServerProviderTelemetrySampleRecord> latest = _TelemetryLatestCache.GetProviders(requestContext.TenantId, clientId);
                if (latest.Count == 0 && _Database?.ClientProviderTelemetry != null)
                {
                    latest = await _Database.ClientProviderTelemetry.LatestProviderByClientAsync(requestContext.TenantId, clientId, token).ConfigureAwait(false);
                }

                await SendJsonAsync(context, latest).ConfigureAwait(false);
                return;
            }

            if (method == "POST" && IsPath(path, "/v1.0/api/client-telemetry/collect", "/v1.0/api/client-telemetry/providers/collect"))
            {
                await RequirePermissionAsync(requestContext, "ClientTelemetry", OperationTypeEnum.Write, token).ConfigureAwait(false);
                ServerClientTelemetryCollectRequest body = ReadJsonBody<ServerClientTelemetryCollectRequest>(context);
                bool collectHostTelemetry = body.CollectHostTelemetry && _Settings.TelemetryCollection.CollectHostTelemetry;
                bool collectProviderTelemetry = body.CollectProviderTelemetry && _Settings.TelemetryCollection.CollectProviderTelemetry;
                ServerClientTelemetryCollectResponse collectResponse = await CollectTelemetryForConnectedClientsAsync(body.ClientId, collectHostTelemetry, collectProviderTelemetry, token).ConfigureAwait(false);
                await SendJsonAsync(context, collectResponse).ConfigureAwait(false);
                return;
            }

            if (method == "POST" && IsPath(path, "/v1.0/api/client-telemetry/history/search"))
            {
                await RequirePermissionAsync(requestContext, "ClientTelemetry", OperationTypeEnum.Read, token).ConfigureAwait(false);
                ServerClientTelemetrySearchRequest body = ReadJsonBody<ServerClientTelemetrySearchRequest>(context);
                body.TenantId = requestContext.TenantId;
                ServerClientTelemetrySearchResult result = _Database?.ClientTelemetry == null ? new ServerClientTelemetrySearchResult() : await _Database.ClientTelemetry.SearchHostAsync(body, token).ConfigureAwait(false);
                await SendJsonAsync(context, result).ConfigureAwait(false);
                return;
            }

            if (method == "POST" && IsPath(path, "/v1.0/api/client-telemetry/providers/history/search"))
            {
                await RequirePermissionAsync(requestContext, "ClientTelemetry", OperationTypeEnum.Read, token).ConfigureAwait(false);
                ServerProviderTelemetrySearchRequest body = ReadJsonBody<ServerProviderTelemetrySearchRequest>(context);
                body.TenantId = requestContext.TenantId;
                ServerProviderTelemetrySearchResult result = _Database?.ClientProviderTelemetry == null ? new ServerProviderTelemetrySearchResult() : await _Database.ClientProviderTelemetry.SearchProviderAsync(body, token).ConfigureAwait(false);
                await SendJsonAsync(context, result).ConfigureAwait(false);
                return;
            }

            if (method == "POST" && IsPath(path, "/v1.0/api/client-telemetry/history/rollups"))
            {
                await RequirePermissionAsync(requestContext, "ClientTelemetry", OperationTypeEnum.Read, token).ConfigureAwait(false);
                ServerClientTelemetryRollupRequest body = ReadJsonBody<ServerClientTelemetryRollupRequest>(context);
                body.TenantId = requestContext.TenantId;
                IReadOnlyList<ServerClientTelemetryRollupBucket> result = _Database?.ClientTelemetry == null ? Array.Empty<ServerClientTelemetryRollupBucket>() : await _Database.ClientTelemetry.RollupHostAsync(body, token).ConfigureAwait(false);
                await SendJsonAsync(context, result).ConfigureAwait(false);
                return;
            }

            if (method == "POST" && IsPath(path, "/v1.0/api/client-telemetry/providers/history/rollups"))
            {
                await RequirePermissionAsync(requestContext, "ClientTelemetry", OperationTypeEnum.Read, token).ConfigureAwait(false);
                ServerProviderTelemetryRollupRequest body = ReadJsonBody<ServerProviderTelemetryRollupRequest>(context);
                body.TenantId = requestContext.TenantId;
                IReadOnlyList<ServerProviderTelemetryRollupBucket> result = _Database?.ClientProviderTelemetry == null ? Array.Empty<ServerProviderTelemetryRollupBucket>() : await _Database.ClientProviderTelemetry.RollupProviderAsync(body, token).ConfigureAwait(false);
                await SendJsonAsync(context, result).ConfigureAwait(false);
                return;
            }

            if (method == "GET" && IsPath(path, "/v1.0/api/client-telemetry/collection-attempts"))
            {
                await RequirePermissionAsync(requestContext, "ClientTelemetry", OperationTypeEnum.Read, token).ConfigureAwait(false);
                string? clientId = context.Request.Query.Elements.Get("clientId");
                IReadOnlyList<ServerClientTelemetryCollectionAttemptRecord> attempts = _Database?.ClientTelemetryCollectionAttempts == null
                    ? Array.Empty<ServerClientTelemetryCollectionAttemptRecord>()
                    : await _Database.ClientTelemetryCollectionAttempts.SearchAttemptsAsync(requestContext.TenantId, clientId, 100, token).ConfigureAwait(false);
                await SendJsonAsync(context, attempts).ConfigureAwait(false);
                return;
            }

            if (method == "GET" && IsPath(path, "/v1.0/api/client-telemetry/metrics"))
            {
                await RequirePermissionAsync(requestContext, "ClientTelemetry", OperationTypeEnum.Read, token).ConfigureAwait(false);
                await SendJsonAsync(context, BuildTelemetryMetricCatalog()).ConfigureAwait(false);
                return;
            }

            if (method == "GET" && TryGetPathId(path, "/v1.0/api/client-telemetry/history/") is string hostTelemetryId)
            {
                await RequirePermissionAsync(requestContext, "ClientTelemetry", OperationTypeEnum.Read, token).ConfigureAwait(false);
                ServerClientTelemetrySampleDetail? detail = _Database?.ClientTelemetry == null ? null : await _Database.ClientTelemetry.ReadHostDetailAsync(requestContext.TenantId, hostTelemetryId, token).ConfigureAwait(false);
                if (detail == null) throw new KeyNotFoundException("Telemetry sample was not found.");
                await SendJsonAsync(context, detail).ConfigureAwait(false);
                return;
            }

            if (method == "DELETE" && TryGetPathId(path, "/v1.0/api/client-telemetry/history/") is string deleteHostTelemetryId)
            {
                await RequirePermissionAsync(requestContext, "ClientTelemetry", OperationTypeEnum.Delete, token).ConfigureAwait(false);
                if (_Database?.ClientTelemetry != null) await _Database.ClientTelemetry.DeleteHostAsync(requestContext.TenantId, deleteHostTelemetryId, token).ConfigureAwait(false);
                await SendNoContentAsync(context).ConfigureAwait(false);
                return;
            }

            if (method == "GET" && TryGetPathId(path, "/v1.0/api/client-telemetry/providers/history/") is string providerTelemetryId)
            {
                await RequirePermissionAsync(requestContext, "ClientTelemetry", OperationTypeEnum.Read, token).ConfigureAwait(false);
                ServerProviderTelemetrySampleDetail? detail = _Database?.ClientProviderTelemetry == null ? null : await _Database.ClientProviderTelemetry.ReadProviderDetailAsync(requestContext.TenantId, providerTelemetryId, token).ConfigureAwait(false);
                if (detail == null) throw new KeyNotFoundException("Provider telemetry sample was not found.");
                await SendJsonAsync(context, detail).ConfigureAwait(false);
                return;
            }

            if (method == "DELETE" && TryGetPathId(path, "/v1.0/api/client-telemetry/providers/history/") is string deleteProviderTelemetryId)
            {
                await RequirePermissionAsync(requestContext, "ClientTelemetry", OperationTypeEnum.Delete, token).ConfigureAwait(false);
                if (_Database?.ClientProviderTelemetry != null) await _Database.ClientProviderTelemetry.DeleteProviderAsync(requestContext.TenantId, deleteProviderTelemetryId, token).ConfigureAwait(false);
                await SendNoContentAsync(context).ConfigureAwait(false);
                return;
            }

            if (method == "POST" && IsPath(path, "/v1.0/api/client-telemetry/history/delete"))
            {
                await RequirePermissionAsync(requestContext, "ClientTelemetry", OperationTypeEnum.Delete, token).ConfigureAwait(false);
                ServerClientTelemetryDeleteRequest body = ReadJsonBody<ServerClientTelemetryDeleteRequest>(context);
                body.TenantId = requestContext.TenantId;
                int deleted = _Database?.ClientTelemetry == null ? 0 : await _Database.ClientTelemetry.DeleteHostBulkAsync(body, token).ConfigureAwait(false);
                await SendJsonAsync(context, new { deleted }).ConfigureAwait(false);
                return;
            }

            if (method == "POST" && IsPath(path, "/v1.0/api/client-telemetry/providers/history/delete"))
            {
                await RequirePermissionAsync(requestContext, "ClientTelemetry", OperationTypeEnum.Delete, token).ConfigureAwait(false);
                ServerClientTelemetryDeleteRequest body = ReadJsonBody<ServerClientTelemetryDeleteRequest>(context);
                body.TenantId = requestContext.TenantId;
                int deleted = _Database?.ClientProviderTelemetry == null ? 0 : await _Database.ClientProviderTelemetry.DeleteProviderBulkAsync(body, token).ConfigureAwait(false);
                await SendJsonAsync(context, new { deleted }).ConfigureAwait(false);
                return;
            }

            if (method == "GET" && IsPath(path, "/api/v1/modeldefinitions", "/v1.0/api/model-definitions"))
            {
                await SendJsonAsync(context, RequirePlatform().ModelDefinitions.Where(item => item.TenantId == requestContext.TenantId).ToList()).ConfigureAwait(false);
                return;
            }

            if (method == "GET" && IsPath(path, "/api/v1/modelconfigurations", "/v1.0/api/model-configurations"))
            {
                await SendJsonAsync(context, RequirePlatform().ModelConfigurations.Where(item => item.TenantId == requestContext.TenantId).ToList()).ConfigureAwait(false);
                return;
            }

            if (method == "GET" && IsPath(path, "/api/v1/routingpolicies", "/v1.0/api/routing-policies"))
            {
                await RequirePermissionAsync(requestContext, "RoutingPolicies", OperationTypeEnum.Read, token).ConfigureAwait(false);
                await SendJsonAsync(context, RequirePlatform().RoutingPolicies.Where(item => item.TenantId == requestContext.TenantId).OrderByDescending(item => item.IsDefault).ThenBy(item => item.ModelName).ThenBy(item => item.ClientName).ToList()).ConfigureAwait(false);
                return;
            }

            if (method == "POST" && IsPath(path, "/v1.0/api/routing-policies/validate"))
            {
                await RequirePermissionAsync(requestContext, "RoutingPolicies", OperationTypeEnum.Read, token).ConfigureAwait(false);
                RoutingPolicySaveRequest body = ReadJsonBody<RoutingPolicySaveRequest>(context);
                RoutingPolicyValidationResult result = ValidateRoutingPolicySaveRequest(body);
                await SendJsonAsync(context, result).ConfigureAwait(false);
                return;
            }

            if (method == "POST" && IsPath(path, "/api/v1/routingpolicies", "/v1.0/api/routing-policies"))
            {
                await RequirePermissionAsync(requestContext, "RoutingPolicies", OperationTypeEnum.Write, token).ConfigureAwait(false);
                RoutingPolicy policy = CreateRoutingPolicy(requestContext, context);
                RequirePlatform().RoutingPolicies.Add(policy);
                await SavePlatformAsync(token).ConfigureAwait(false);
                await SendJsonAsync(context, policy).ConfigureAwait(false);
                return;
            }

            if (method == "PUT" && TryGetPathId(path, "/api/v1/routingpolicies/", "/v1.0/api/routing-policies/") is string updateRoutingPolicyId)
            {
                await RequirePermissionAsync(requestContext, "RoutingPolicies", OperationTypeEnum.Write, token).ConfigureAwait(false);
                RoutingPolicy policy = UpdateRoutingPolicy(requestContext, updateRoutingPolicyId, context);
                await SavePlatformAsync(token).ConfigureAwait(false);
                await SendJsonAsync(context, policy).ConfigureAwait(false);
                return;
            }

            if (method == "DELETE" && TryGetPathId(path, "/api/v1/routingpolicies/", "/v1.0/api/routing-policies/") is string deleteRoutingPolicyId)
            {
                await RequirePermissionAsync(requestContext, "RoutingPolicies", OperationTypeEnum.Delete, token).ConfigureAwait(false);
                int removed = DeleteRoutingPolicy(requestContext, deleteRoutingPolicyId);
                if (removed > 0) await SavePlatformAsync(token).ConfigureAwait(false);
                await SendNoContentAsync(context).ConfigureAwait(false);
                return;
            }

            if (method == "GET" && IsPath(path, "/v1/models", "/api/v1/openai/v1/models"))
            {
                await RequirePermissionAsync(requestContext, "Inference", OperationTypeEnum.Read, token).ConfigureAwait(false);
                List<object> models = RequirePlatform().Clients
                    .Where(item => item.TenantId == requestContext.TenantId)
                    .SelectMany(item => item.Models)
                    .GroupBy(item => item.Name, StringComparer.OrdinalIgnoreCase)
                    .Select(group => (object)new { id = group.Key, @object = "model", created = ToUnixTimeSeconds(group.Max(item => item.LastAdvertisedUtc)), owned_by = "xeno" })
                    .ToList();
                await SendJsonAsync(context, new { @object = "list", data = models }).ConfigureAwait(false);
                return;
            }

            if (method == "GET" && IsPath(path, "/api/tags"))
            {
                await RequirePermissionAsync(requestContext, "Inference", OperationTypeEnum.Read, token).ConfigureAwait(false);
                await SendJsonAsync(context, BuildOllamaTagsResponse(requestContext)).ConfigureAwait(false);
                return;
            }

            if (method == "POST" && IsPath(path, "/v1/chat/completions", "/v1/completions", "/api/v1/openai/v1/chat/completions", "/api/v1/openai/v1/completions"))
            {
                await RequirePermissionAsync(requestContext, "Inference", OperationTypeEnum.Execute, token).ConfigureAwait(false);
                await HandleCompletionAsync(context, requestContext, path, false, token).ConfigureAwait(false);
                return;
            }

            if (method == "POST" && IsPath(path, "/v1/embeddings", "/api/v1/openai/v1/embeddings"))
            {
                await RequirePermissionAsync(requestContext, "Inference", OperationTypeEnum.Execute, token).ConfigureAwait(false);
                await HandleEmbeddingAsync(context, requestContext, path, token).ConfigureAwait(false);
                return;
            }

            if (method == "POST" && IsPath(path, "/api/chat", "/api/generate"))
            {
                await RequirePermissionAsync(requestContext, "Inference", OperationTypeEnum.Execute, token).ConfigureAwait(false);
                await HandleOllamaCompletionAsync(context, requestContext, path, token).ConfigureAwait(false);
                return;
            }

            if (method == "POST" && IsPath(path, "/api/embed", "/api/embeddings"))
            {
                await RequirePermissionAsync(requestContext, "Inference", OperationTypeEnum.Execute, token).ConfigureAwait(false);
                await HandleOllamaEmbeddingAsync(context, requestContext, path, token).ConfigureAwait(false);
                return;
            }

            if (method == "GET" && IsPath(path, "/v1.0/api/ledger/status"))
            {
                await RequirePermissionAsync(requestContext, "Ledger", OperationTypeEnum.Read, token).ConfigureAwait(false);
                LedgerStatusResponse response = _Ledger == null ? new LedgerStatusResponse() : await _Ledger.GetStatusAsync(token).ConfigureAwait(false);
                await SendJsonAsync(context, response).ConfigureAwait(false);
                return;
            }

            if (method == "GET" && IsPath(path, "/v1.0/api/ledger/balance/me"))
            {
                await RequirePermissionAsync(requestContext, "Ledger", OperationTypeEnum.Read, token).ConfigureAwait(false);
                string userId = ResolveContextUserId(requestContext);
                await SendJsonAsync(context, BuildLedgerBalance(requestContext.TenantId, userId)).ConfigureAwait(false);
                return;
            }

            if (method == "GET" && IsPath(path, "/v1.0/api/ledger/balances"))
            {
                await RequirePermissionAsync(requestContext, "Ledger", OperationTypeEnum.Read, token).ConfigureAwait(false);
                LedgerBalanceSearchRequest search = BuildLedgerBalanceSearchRequest(requestContext, context);
                await SendJsonAsync(context, SearchLedgerBalances(requestContext, search)).ConfigureAwait(false);
                return;
            }

            if (method == "GET" && TryGetPathId(path, "/v1.0/api/ledger/balances/users/") is string ledgerUserId)
            {
                await RequirePermissionAsync(requestContext, "Ledger", OperationTypeEnum.Read, token).ConfigureAwait(false);
                EnsureCanReadLedgerUser(requestContext, requestContext.TenantId, ledgerUserId);
                await SendJsonAsync(context, BuildLedgerBalance(requestContext.TenantId, ledgerUserId)).ConfigureAwait(false);
                return;
            }

            if (method == "GET" && IsPath(path, "/v1.0/api/ledger/entries/me"))
            {
                await RequirePermissionAsync(requestContext, "Ledger", OperationTypeEnum.Read, token).ConfigureAwait(false);
                string userId = ResolveContextUserId(requestContext);
                LedgerEntrySearchRequest search = new LedgerEntrySearchRequest { TenantId = requestContext.TenantId, UserId = userId };
                await SendJsonAsync(context, SearchLedgerEntries(requestContext, search)).ConfigureAwait(false);
                return;
            }

            if (method == "POST" && IsPath(path, "/v1.0/api/ledger/entries/search"))
            {
                await RequirePermissionAsync(requestContext, "Ledger", OperationTypeEnum.Read, token).ConfigureAwait(false);
                LedgerEntrySearchRequest search = ReadJsonBody<LedgerEntrySearchRequest>(context);
                await SendJsonAsync(context, SearchLedgerEntries(requestContext, search)).ConfigureAwait(false);
                return;
            }

            if (method == "POST" && IsPath(path, "/v1.0/api/ledger/drain"))
            {
                await RequirePermissionAsync(requestContext, "Ledger", OperationTypeEnum.Admin, token).ConfigureAwait(false);
                LedgerDrainResponse response = _Ledger == null ? new LedgerDrainResponse() : await _Ledger.DrainOnceAsync(token).ConfigureAwait(false);
                _Authentication.AppendAudit(RequirePlatform(), requestContext, "Ledger.Drain", OperationTypeEnum.Admin, true, "LedgerDrainTriggered", 200, requestContext.TenantId, null);
                await SavePlatformAsync(token).ConfigureAwait(false);
                await SendJsonAsync(context, response).ConfigureAwait(false);
                return;
            }

            if (method == "POST" && IsPath(path, "/v1.0/api/ledger/reconcile"))
            {
                await RequirePermissionAsync(requestContext, "Ledger", OperationTypeEnum.Read, token).ConfigureAwait(false);
                _Authentication.AppendAudit(RequirePlatform(), requestContext, "Ledger.Reconcile", OperationTypeEnum.Admin, true, "LedgerReconcileTriggered", 200, requestContext.TenantId, null);
                await SavePlatformAsync(token).ConfigureAwait(false);
                await SendJsonAsync(context, BuildLedgerReconcileResponse(requestContext)).ConfigureAwait(false);
                return;
            }

            if (method == "GET" && IsPath(path, "/api/v1/requesthistory", "/v1.0/api/request-history"))
            {
                await RequirePermissionAsync(requestContext, "RequestHistory", OperationTypeEnum.Read, token).ConfigureAwait(false);
                await SendJsonAsync(context, ListRequestHistory(requestContext, context)).ConfigureAwait(false);
                return;
            }

            if (method == "GET" && IsPath(path, "/api/v1/requesthistory/summary", "/v1.0/api/request-history/summary"))
            {
                await RequirePermissionAsync(requestContext, "RequestHistory", OperationTypeEnum.Read, token).ConfigureAwait(false);
                await SendJsonAsync(context, SummarizeRequestHistory(requestContext, context)).ConfigureAwait(false);
                return;
            }

            if (method == "GET" && IsPath(path, "/api/v1/requesthistory/analytics/overview", "/v1.0/api/request-history/analytics/overview", "/api/v1/analytics/overview", "/v1.0/api/analytics/overview"))
            {
                await RequirePermissionAsync(requestContext, "RequestHistory", OperationTypeEnum.Read, token).ConfigureAwait(false);
                await SendJsonAsync(context, AnalyticsOverview(requestContext, context)).ConfigureAwait(false);
                return;
            }

            if (method == "POST" && IsPath(path, "/api/v1/requesthistory/bulk", "/v1.0/api/request-history/bulk"))
            {
                await RequirePermissionAsync(requestContext, "RequestHistory", OperationTypeEnum.Delete, token).ConfigureAwait(false);
                RequestHistoryDeleteRequest body = ReadJsonBody<RequestHistoryDeleteRequest>(context);
                int removed = DeleteRequestHistory(requestContext, body);
                await SavePlatformAsync(token).ConfigureAwait(false);
                await SendJsonAsync(context, new { removed }).ConfigureAwait(false);
                return;
            }

            if (method == "GET" && IsPath(path, "/api/v1/settings", "/v1.0/api/settings"))
            {
                EnsureSystemAdmin(requestContext, requestContext.TenantId, null);
                await SendJsonAsync(context, _Settings).ConfigureAwait(false);
                return;
            }

            if (method == "POST" && IsPath(path, "/api/v1/settings", "/v1.0/api/settings"))
            {
                EnsureSystemAdmin(requestContext, requestContext.TenantId, null);
                XenoSettings? incoming = JsonUtil.Deserialize<XenoSettings>(context.Request.DataAsString ?? "{}");
                if (incoming == null) throw new ArgumentException("Settings payload is invalid.");
                ApplySettings(incoming);
                await SaveSettingsAsync(token).ConfigureAwait(false);
                await SendJsonAsync(context, _Settings).ConfigureAwait(false);
                return;
            }

            if (method == "POST" && IsPath(path, "/api/v1/email/test", "/v1.0/api/email/test"))
            {
                EnsureSystemAdmin(requestContext, requestContext.TenantId, null);
                EmailTestRequest body = ReadJsonBody<EmailTestRequest>(context);
                EmailTestResponse response = await SendAdministrativeTestEmailAsync(body, requestContext, token).ConfigureAwait(false);
                await SendJsonAsync(context, response).ConfigureAwait(false);
                return;
            }

            if (method == "GET" && IsPath(path, "/api/v1/backup", "/v1.0/api/backup"))
            {
                EnsureSystemAdmin(requestContext, requestContext.TenantId, null);
                await SendJsonAsync(context, RequirePlatform().ToState()).ConfigureAwait(false);
                return;
            }

            if (method == "POST" && IsPath(path, "/api/v1/restore", "/v1.0/api/restore"))
            {
                EnsureSystemAdmin(requestContext, requestContext.TenantId, null);
                PlatformState? state = JsonUtil.Deserialize<PlatformState>(context.Request.DataAsString ?? "{}");
                if (state == null) throw new ArgumentException("Restore payload is invalid.");
                RequirePlatform().LoadState(state);
                await SavePlatformAsync(token).ConfigureAwait(false);
                await SendJsonAsync(context, new { restored = true }).ConfigureAwait(false);
                return;
            }

            if (method == "GET" && TryGetPathId(path, "/api/v1/requesthistory/", "/v1.0/api/request-history/") is string requestHistoryId)
            {
                await RequirePermissionAsync(requestContext, "RequestHistory", OperationTypeEnum.Read, token).ConfigureAwait(false);
                RequestHistoryEntry? entry = RequirePlatform().RequestHistory.FirstOrDefault(item => item.Id == requestHistoryId && item.TenantId == requestContext.TenantId);
                if (entry == null) throw new KeyNotFoundException("Request history entry not found.");
                await SendJsonAsync(context, entry).ConfigureAwait(false);
                return;
            }

            if (method == "DELETE" && TryGetPathId(path, "/api/v1/requesthistory/", "/v1.0/api/request-history/") is string deleteRequestHistoryId)
            {
                await RequirePermissionAsync(requestContext, "RequestHistory", OperationTypeEnum.Delete, token).ConfigureAwait(false);
                int removed = RequirePlatform().RequestHistory.RemoveAll(item => item.Id == deleteRequestHistoryId && item.TenantId == requestContext.TenantId);
                if (removed > 0) await SavePlatformAsync(token).ConfigureAwait(false);
                await SendNoContentAsync(context).ConfigureAwait(false);
                return;
            }

            context.Response.StatusCode = 404;
            await SendJsonAsync(context, new { error = "NotFound", message = "Route not found." }).ConfigureAwait(false);
        }

        private async Task HandleCompletionAsync(HttpContextBase context, RequestContext requestContext, string path, bool stream, CancellationToken token)
        {
            string requestBodyJson = ReadBodyJson(context);
            OpenAiRequestBody body = ReadJsonBody<OpenAiRequestBody>(context);
            bool requestedStream = stream || body.Stream.GetValueOrDefault();
            string? model = body.Model;
            RoutingDecision decision = await RequireRouting().RouteAsync(requestContext, model, false, token).ConfigureAwait(false);

            if (!decision.Success)
            {
                RequestHistoryEntry history = await RecordAndSaveHistoryAsync(requestContext, context.Request.Method.ToString().ToUpperInvariant(), path, decision, token).ConfigureAwait(false);
                ApplyRoutingTraceHeaders(context, requestContext, decision, null, null, history);
                await SendStatusJsonAsync(context, decision.HttpStatusCode, new { error = decision.OutcomeCode, message = decision.Message }).ConfigureAwait(false);
                return;
            }

            await RelayClientRequestAsync(context, requestContext, path, decision, requestBodyJson, false, requestedStream, token).ConfigureAwait(false);
        }

        private async Task HandleEmbeddingAsync(HttpContextBase context, RequestContext requestContext, string path, CancellationToken token)
        {
            string requestBodyJson = ReadBodyJson(context);
            OpenAiRequestBody body = ReadJsonBody<OpenAiRequestBody>(context);
            string? model = body.Model;
            RoutingDecision decision = await RequireRouting().RouteAsync(requestContext, model, true, token).ConfigureAwait(false);

            if (!decision.Success)
            {
                RequestHistoryEntry history = await RecordAndSaveHistoryAsync(requestContext, context.Request.Method.ToString().ToUpperInvariant(), path, decision, token).ConfigureAwait(false);
                ApplyRoutingTraceHeaders(context, requestContext, decision, null, null, history);
                await SendStatusJsonAsync(context, decision.HttpStatusCode, new { error = decision.OutcomeCode, message = decision.Message }).ConfigureAwait(false);
                return;
            }

            await RelayClientRequestAsync(context, requestContext, path, decision, requestBodyJson, true, false, token).ConfigureAwait(false);
        }

        private async Task HandleOllamaCompletionAsync(HttpContextBase context, RequestContext requestContext, string path, CancellationToken token)
        {
            string requestBodyJson = ReadBodyJson(context);
            OpenAiRequestBody body = ReadJsonBody<OpenAiRequestBody>(context);
            bool requestedStream = body.Stream.GetValueOrDefault();
            RoutingDecision decision = await RequireRouting().RouteAsync(requestContext, body.Model, false, ApiProviderTypeEnum.Ollama, token).ConfigureAwait(false);

            if (!decision.Success)
            {
                RequestHistoryEntry history = await RecordAndSaveHistoryAsync(requestContext, context.Request.Method.ToString().ToUpperInvariant(), path, decision, token).ConfigureAwait(false);
                ApplyRoutingTraceHeaders(context, requestContext, decision, null, null, history);
                await SendStatusJsonAsync(context, decision.HttpStatusCode, new { error = decision.Message }).ConfigureAwait(false);
                return;
            }

            await RelayClientRequestAsync(context, requestContext, path, decision, requestBodyJson, false, requestedStream, token).ConfigureAwait(false);
        }

        private async Task HandleOllamaEmbeddingAsync(HttpContextBase context, RequestContext requestContext, string path, CancellationToken token)
        {
            string requestBodyJson = ReadBodyJson(context);
            OpenAiRequestBody body = ReadJsonBody<OpenAiRequestBody>(context);
            RoutingDecision decision = await RequireRouting().RouteAsync(requestContext, body.Model, true, ApiProviderTypeEnum.Ollama, token).ConfigureAwait(false);

            if (!decision.Success)
            {
                RequestHistoryEntry history = await RecordAndSaveHistoryAsync(requestContext, context.Request.Method.ToString().ToUpperInvariant(), path, decision, token).ConfigureAwait(false);
                ApplyRoutingTraceHeaders(context, requestContext, decision, null, null, history);
                await SendStatusJsonAsync(context, decision.HttpStatusCode, new { error = decision.Message }).ConfigureAwait(false);
                return;
            }

            await RelayClientRequestAsync(context, requestContext, path, decision, requestBodyJson, true, false, token).ConfigureAwait(false);
        }

        private async Task RelayClientRequestAsync(
            HttpContextBase context,
            RequestContext requestContext,
            string path,
            RoutingDecision decision,
            string requestBodyJson,
            bool embeddings,
            bool stream,
            CancellationToken token)
        {
            XenoClientRecord? client = RequirePlatform().Clients.FirstOrDefault(item => item.Id == decision.SelectedClientId && item.TenantId == requestContext.TenantId);
            if (client == null)
            {
                decision.Success = false;
                decision.HttpStatusCode = 502;
                decision.OutcomeCode = "ClientMissing";
                decision.Message = "The selected client no longer exists.";
                decision.Timeline.Add("ClientMissing");
                RequestHistoryEntry history = await RecordAndSaveHistoryAsync(requestContext, context.Request.Method.ToString().ToUpperInvariant(), path, decision, token).ConfigureAwait(false);
                ApplyRoutingTraceHeaders(context, requestContext, decision, null, null, history);
                await SendStatusJsonAsync(context, decision.HttpStatusCode, new { error = decision.OutcomeCode, message = decision.Message }).ConfigureAwait(false);
                return;
            }

            ClientConnection? connection = GetClientConnection(decision.SelectedClientId);
            if (connection == null || !connection.Session.IsConnected)
            {
                decision.Success = false;
                decision.HttpStatusCode = 503;
                decision.OutcomeCode = "ClientNotConnected";
                decision.Message = "The selected client is not connected.";
                decision.Timeline.Add("ClientNotConnected");
                RequestHistoryEntry history = await RecordAndSaveHistoryAsync(requestContext, context.Request.Method.ToString().ToUpperInvariant(), path, decision, token).ConfigureAwait(false);
                ApplyRoutingTraceHeaders(context, requestContext, decision, client, null, history);
                await SendStatusJsonAsync(context, decision.HttpStatusCode, new { error = decision.OutcomeCode, message = decision.Message }).ConfigureAwait(false);
                return;
            }

            string requestId = requestContext.RequestId;
            PendingClientResponse pending = new PendingClientResponse();
            lock (_PendingResponsesLock)
            {
                _PendingClientResponses[requestId] = pending;
            }

            XenoClientModel? selectedModel = FindSelectedModel(client, decision, embeddings);
            client.InFlightRequests++;
            DateTime? clientRequestStartedUtc = null;
            bool requestDispatched = false;
            string dispatchedMethod = context.Request.Method.ToString().ToUpperInvariant();
            string dispatchedRelativePath = NormalizeOpenAiPath(path);
            string dispatchedRequestorIp = String.Empty;
            string? dispatchedSource = selectedModel?.Source;
            string requestEventKey = RequestEventKey(context.Connection.Guid, context.Stream.Guid);
            CancellationTokenSource? callerDisconnectedSource = null;
            CancellationTokenSource? requestWaitSource = null;
            try
            {
                callerDisconnectedSource = new CancellationTokenSource();
                requestWaitSource = CancellationTokenSource.CreateLinkedTokenSource(token, callerDisconnectedSource.Token);
                _PendingCallerDisconnects[requestEventKey] = callerDisconnectedSource;

                string relativePath = dispatchedRelativePath;
                string requestorIp = GetRequestorIp(context, requestContext);
                dispatchedRequestorIp = requestorIp;
                Dictionary<string, string> forwardedHeaders = BuildForwardedHeaders(context, requestContext, requestorIp);
                Dictionary<string, string> capturedRequestHeaders = CaptureRequestHeaders(context, forwardedHeaders);
                ServerRequestMessage payload = new ServerRequestMessage
                {
                    Type = "server.request",
                    RequestId = requestId,
                    TenantId = requestContext.TenantId,
                    RequestorTenantId = requestContext.TenantId,
                    RequestorPrincipalType = requestContext.PrincipalType.ToString(),
                    RequestorPrincipalId = requestContext.PrincipalId,
                    ClientId = client.Id,
                    ClientName = client.Name,
                    ApiType = selectedModel?.Provider.ToString(),
                    Source = dispatchedSource,
                    Method = dispatchedMethod,
                    RelativePath = relativePath,
                    Model = decision.EffectiveModel,
                    Embeddings = embeddings,
                    Stream = stream,
                    RequestorIp = requestorIp,
                    Headers = forwardedHeaders,
                    Body = requestBodyJson
                };

                clientRequestStartedUtc = DateTime.UtcNow;
                decision.Timeline.Add("ClientDispatch");
                await SendToClientAsync(connection, JsonUtil.Serialize(payload), requestWaitSource.Token).ConfigureAwait(false);
                requestDispatched = true;
                if (stream)
                {
                    ClientResponseMessage firstResponse = await WaitForClientFirstResponseAsync(requestId, pending, requestWaitSource.Token).ConfigureAwait(false);
                    if (IsClientResponseStart(firstResponse))
                    {
                        await StreamClientResponseAsync(context, requestContext, path, decision, client, selectedModel, pending, firstResponse, requestBodyJson, capturedRequestHeaders, clientRequestStartedUtc.Value, requestWaitSource.Token).ConfigureAwait(false);
                    }
                    else
                    {
                        decision.ClientRoundTripMs = Math.Max(0, (long)(DateTime.UtcNow - clientRequestStartedUtc.Value).TotalMilliseconds);
                        decision.Timeline.Add("ClientResponse");
                        await SendClientResponseAsync(context, requestContext, path, decision, client, selectedModel, firstResponse, requestBodyJson, capturedRequestHeaders, requestWaitSource.Token).ConfigureAwait(false);
                    }
                }
                else
                {
                    ClientResponseMessage response = await WaitForClientResponseAsync(requestId, pending, requestWaitSource.Token).ConfigureAwait(false);
                    decision.ClientRoundTripMs = Math.Max(0, (long)(DateTime.UtcNow - clientRequestStartedUtc.Value).TotalMilliseconds);
                    decision.Timeline.Add("ClientResponse");
                    await SendClientResponseAsync(context, requestContext, path, decision, client, selectedModel, response, requestBodyJson, capturedRequestHeaders, requestWaitSource.Token).ConfigureAwait(false);
                }
            }
            catch (OperationCanceledException) when (callerDisconnectedSource != null && callerDisconnectedSource.IsCancellationRequested)
            {
                decision.Success = false;
                decision.HttpStatusCode = 499;
                decision.OutcomeCode = "CallerDisconnected";
                decision.Message = "The caller disconnected before the client response completed.";
                if (clientRequestStartedUtc.HasValue) decision.ClientRoundTripMs = Math.Max(0, (long)(DateTime.UtcNow - clientRequestStartedUtc.Value).TotalMilliseconds);
                decision.Timeline.Add("CallerDisconnected");
                if (requestDispatched)
                {
                    try
                    {
                        await SendRequestCancellationAsync(connection, requestId, "CallerDisconnected", dispatchedRequestorIp, dispatchedMethod, dispatchedRelativePath, dispatchedSource, CancellationToken.None).ConfigureAwait(false);
                    }
                    catch (Exception ex) when (ex is InvalidOperationException || ex is IOException || ex is WebSocketException)
                    {
                    }
                }

                await RecordAndSaveHistoryAsync(requestContext, dispatchedMethod, path, decision, CancellationToken.None).ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
                decision.Success = false;
                decision.HttpStatusCode = 504;
                decision.OutcomeCode = "ClientTimeout";
                decision.Message = "The selected client did not respond before the timeout.";
                if (clientRequestStartedUtc.HasValue) decision.ClientRoundTripMs = Math.Max(0, (long)(DateTime.UtcNow - clientRequestStartedUtc.Value).TotalMilliseconds);
                decision.Timeline.Add("ClientTimeout");
                RequestHistoryEntry history = await RecordAndSaveHistoryAsync(requestContext, context.Request.Method.ToString().ToUpperInvariant(), path, decision, CancellationToken.None).ConfigureAwait(false);
                ApplyRoutingTraceHeaders(context, requestContext, decision, client, selectedModel, history);
                await SendStatusJsonAsync(context, decision.HttpStatusCode, new { error = decision.OutcomeCode, message = decision.Message }).ConfigureAwait(false);
            }
            finally
            {
                client.InFlightRequests = Math.Max(0, client.InFlightRequests - 1);
                lock (_PendingResponsesLock)
                {
                    _PendingClientResponses.Remove(requestId);
                }
                _PendingCallerDisconnects.TryRemove(requestEventKey, out CancellationTokenSource? _);
                requestWaitSource?.Dispose();
                callerDisconnectedSource?.Dispose();
                await SavePlatformAsync(CancellationToken.None).ConfigureAwait(false);
            }
        }

        private async Task SendClientResponseAsync(
            HttpContextBase context,
            RequestContext requestContext,
            string path,
            RoutingDecision decision,
            XenoClientRecord client,
            XenoClientModel? selectedModel,
            ClientResponseMessage response,
            string requestBodyJson,
            Dictionary<string, string> requestHeaders,
            CancellationToken token)
        {
            bool success = response.Success;
            int statusCode = response.StatusCode ?? (success ? 200 : 502);

            decision.Success = success;
            decision.HttpStatusCode = statusCode;
            decision.OutcomeCode = success ? "Completed" : "ClientRequestFailed";
            decision.Message = success ? "Client completed the routed request." : response.Error ?? "Client request failed.";

            context.Response.StatusCode = statusCode;
            string contentType = response.ContentType ?? "application/json";
            context.Response.ContentType = contentType;
            Dictionary<string, string> responseHeaders = ExtractHeaders(response);
            ApplyClientResponseHeaders(context, responseHeaders);
            string body = response.Body ?? "{}";
            RequestHistoryEntry history = await RecordAndSaveHistoryAsync(
                requestContext,
                context.Request.Method.ToString().ToUpperInvariant(),
                path,
                decision,
                token,
                entry => PopulateRequestHistory(entry, requestContext, requestBodyJson, requestHeaders, body, responseHeaders, response, decision)).ConfigureAwait(false);
            ApplyRoutingTraceHeaders(context, requestContext, decision, client, selectedModel, history);
            await context.Response.Send(body, context.Token).ConfigureAwait(false);
        }

        private async Task StreamClientResponseAsync(
            HttpContextBase context,
            RequestContext requestContext,
            string path,
            RoutingDecision decision,
            XenoClientRecord client,
            XenoClientModel? selectedModel,
            PendingClientResponse pending,
            ClientResponseMessage startMessage,
            string requestBodyJson,
            Dictionary<string, string> requestHeaders,
            DateTime clientRequestStartedUtc,
            CancellationToken token)
        {
            bool success = startMessage.Success;
            int statusCode = startMessage.StatusCode ?? (success ? 200 : 502);

            decision.Success = success;
            decision.HttpStatusCode = statusCode;
            decision.OutcomeCode = success ? "Streaming" : "ClientRequestFailed";
            decision.Message = success ? "Client started streaming the routed request." : startMessage.Error ?? "Client request failed.";
            decision.Timeline.Add("ClientResponseStart");

            Dictionary<string, string> responseHeaders = ExtractHeaders(startMessage);
            RequestHistoryEntry history = RequireRouting().RecordHistory(requestContext, path, context.Request.Method.ToString().ToUpperInvariant(), decision);
            PopulateRequestHistory(history, requestContext, requestBodyJson, requestHeaders, null, responseHeaders, startMessage, decision);

            context.Response.StatusCode = statusCode;
            string contentType = startMessage.ContentType ?? "application/json";
            context.Response.ContentType = contentType;
            context.Response.ChunkedTransfer = true;
            ApplyClientResponseHeaders(context, responseHeaders);
            ApplyRoutingTraceHeaders(context, requestContext, decision, client, selectedModel, history);

            using (CancellationTokenSource timeout = CancellationTokenSource.CreateLinkedTokenSource(token))
            {
                timeout.CancelAfter(TimeSpan.FromMilliseconds(Math.Max(1000, _Settings.WebSocket.RequestTimeoutMs)));
                using (timeout.Token.Register(delegate { pending.Cancel(timeout.Token); }))
                {
                    while (await pending.Chunks.Reader.WaitToReadAsync(timeout.Token).ConfigureAwait(false))
                    {
                        while (pending.Chunks.Reader.TryRead(out ClientResponseChunkMessage? chunk))
                        {
                            if (chunk == null) continue;
                            byte[] chunkBytes = chunk.Decode();
                            if (chunkBytes.Length < 1) continue;
                            await context.Response.SendChunk(chunkBytes, false, context.Token).ConfigureAwait(false);
                        }
                    }

                    ClientResponseMessage finalMessage = await pending.Final.Task.ConfigureAwait(false);
                    decision.ClientRoundTripMs = Math.Max(0, (long)(DateTime.UtcNow - clientRequestStartedUtc).TotalMilliseconds);
                    decision.Timeline.Add("ClientResponseComplete");

                    bool finalSuccess = finalMessage.Success;
                    if (finalMessage.StatusCode.HasValue) statusCode = finalMessage.StatusCode.Value;

                    decision.Success = finalSuccess;
                    decision.HttpStatusCode = statusCode;
                    decision.OutcomeCode = finalSuccess ? "Completed" : "ClientRequestFailed";
                    decision.Message = finalSuccess ? "Client completed the streamed routed request." : finalMessage.Error ?? "Client request failed.";
                    string body = finalMessage.Body ?? String.Empty;
                    Dictionary<string, string> finalResponseHeaders = ExtractHeaders(finalMessage);
                    if (finalResponseHeaders.Count > 0) responseHeaders = finalResponseHeaders;
                    PopulateRequestHistory(history, requestContext, requestBodyJson, requestHeaders, body, responseHeaders, finalMessage, decision);
                    TryEnqueueLedgerEvent(history);
                }
            }

            await SavePlatformAsync(CancellationToken.None).ConfigureAwait(false);
            await context.Response.SendChunk(Array.Empty<byte>(), true, context.Token).ConfigureAwait(false);
        }

        private async Task SendToClientAsync(ClientConnection connection, string payload, CancellationToken token)
        {
            await connection.SendLock.WaitAsync(token).ConfigureAwait(false);
            try
            {
                await connection.Session.SendTextAsync(payload, token).ConfigureAwait(false);
            }
            finally
            {
                connection.SendLock.Release();
            }
        }

        private async Task SendRequestCancellationAsync(
            ClientConnection connection,
            string requestId,
            string reason,
            string requestorIp,
            string method,
            string relativePath,
            string? source,
            CancellationToken token)
        {
            ServerRequestCancellationMessage message = new ServerRequestCancellationMessage
            {
                Type = "server.request.cancel",
                RequestId = requestId,
                Reason = reason,
                RequestorIp = requestorIp,
                Method = method,
                RelativePath = relativePath,
                Source = source
            };
            await SendToClientAsync(connection, JsonUtil.Serialize(message), token).ConfigureAwait(false);
        }

        private void CancelPendingRoutedRequest(RequestEventArgs args)
        {
            if (args == null) return;
            string key = RequestEventKey(args.ConnectionId, args.StreamId);
            if (_PendingCallerDisconnects.TryGetValue(key, out CancellationTokenSource? cancellation))
            {
                cancellation.Cancel();
            }
        }

        private static string RequestEventKey(Guid connectionId, Guid streamId)
        {
            return connectionId.ToString("N", CultureInfo.InvariantCulture) + ":" + streamId.ToString("N", CultureInfo.InvariantCulture);
        }

        private object BuildOllamaTagsResponse(RequestContext requestContext)
        {
            List<object> models = RequirePlatform().Clients
                .Where(item => item.TenantId == requestContext.TenantId)
                .SelectMany(item => item.Models)
                .Where(item => item.Provider == ApiProviderTypeEnum.Ollama)
                .GroupBy(item => item.Name, StringComparer.OrdinalIgnoreCase)
                .Select(group => BuildOllamaTagModel(group.Key, group.Max(item => item.LastAdvertisedUtc)))
                .ToList();
            return new { models };
        }

        private static object BuildOllamaTagModel(string modelName, DateTime modifiedUtc)
        {
            return new
            {
                name = modelName,
                model = modelName,
                modified_at = modifiedUtc.ToUniversalTime().ToString("O", CultureInfo.InvariantCulture),
                size = 0,
                digest = StableDigest(modelName),
                details = new
                {
                    format = String.Empty,
                    family = FamilyFromModelName(modelName),
                    families = new[] { FamilyFromModelName(modelName) },
                    parameter_size = String.Empty,
                    quantization_level = String.Empty
                }
            };
        }

        private static XenoClientModel? FindSelectedModel(XenoClientRecord client, RoutingDecision decision, bool embeddings)
        {
            return client.Models.FirstOrDefault(item =>
                String.Equals(item.Name, decision.EffectiveModel, StringComparison.OrdinalIgnoreCase)
                && String.Equals(item.Provider.ToString(), decision.SelectedProvider, StringComparison.OrdinalIgnoreCase)
                && (String.IsNullOrWhiteSpace(decision.SelectedSource) || String.Equals(item.Source, decision.SelectedSource, StringComparison.OrdinalIgnoreCase))
                && (embeddings ? item.SupportsEmbeddings : item.SupportsCompletions));
        }

        private static long ToUnixTimeSeconds(DateTime value)
        {
            DateTime normalized = value.Kind == DateTimeKind.Unspecified ? DateTime.SpecifyKind(value, DateTimeKind.Utc) : value;
            return new DateTimeOffset(normalized.ToUniversalTime()).ToUnixTimeSeconds();
        }

        private static string StableDigest(string value)
        {
            byte[] bytes = Encoding.UTF8.GetBytes(value ?? String.Empty);
            byte[] hash = SHA256.HashData(bytes);
            return Convert.ToHexString(hash).ToLowerInvariant();
        }

        private static string FamilyFromModelName(string modelName)
        {
            if (String.IsNullOrWhiteSpace(modelName)) return String.Empty;
            int tagIndex = modelName.IndexOf(':');
            return tagIndex > 0 ? modelName.Substring(0, tagIndex) : modelName;
        }

        private void StartTelemetryCollectionLoop()
        {
            if (!_Settings.TelemetryCollection.Enabled) return;
            _TelemetryCollectionCancellation = new CancellationTokenSource();
            _TelemetryCollectionTask = Task.Run(async () => await TelemetryCollectionLoopAsync(_TelemetryCollectionCancellation.Token).ConfigureAwait(false));
        }

        private void StartWebSocketMaintenanceLoop()
        {
            if (!_Settings.WebSocket.Enabled) return;
            _WebSocketMaintenanceCancellation = new CancellationTokenSource();
            _WebSocketMaintenanceTask = Task.Run(async () => await WebSocketMaintenanceLoopAsync(_WebSocketMaintenanceCancellation.Token).ConfigureAwait(false));
        }

        private async Task WebSocketMaintenanceLoopAsync(CancellationToken token)
        {
            try
            {
                TimeSpan interval = TimeSpan.FromMilliseconds(_Settings.WebSocket.StaleConnectionScanIntervalMs);
                using (PeriodicTimer timer = new PeriodicTimer(interval))
                {
                    while (await timer.WaitForNextTickAsync(token).ConfigureAwait(false))
                    {
                        await EvictStaleClientConnectionsAsync(DateTime.UtcNow, token).ConfigureAwait(false);
                    }
                }
            }
            catch (OperationCanceledException) when (token.IsCancellationRequested)
            {
            }
        }

        private async Task EvictStaleClientConnectionsAsync(DateTime utcNow, CancellationToken token)
        {
            List<ClientConnection> staleConnections = new List<ClientConnection>();
            lock (_ClientConnectionsLock)
            {
                foreach (ClientConnection connection in _ClientConnections.Values.ToList())
                {
                    if (!IsClientConnectionStale(connection.ClientId, connection.TenantId, utcNow)) continue;
                    _ClientConnections.Remove(connection.ClientId);
                    staleConnections.Add(connection);
                }
            }

            if (staleConnections.Count == 0) return;
            XenoPlatform platform = RequirePlatform();
            foreach (ClientConnection connection in staleConnections)
            {
                XenoClientRecord? client = platform.Clients.FirstOrDefault(item => item.Id == connection.ClientId && item.TenantId == connection.TenantId);
                if (client != null)
                {
                    client.Connected = false;
                    client.InFlightRequests = 0;
                    client.ServiceState = ClientServiceStateEnum.Quarantined;
                }

                await CloseStaleSessionAsync(connection, token).ConfigureAwait(false);
            }

            await SavePlatformAsync(token).ConfigureAwait(false);
        }

        private bool IsClientConnectionStale(string clientId, string tenantId, DateTime utcNow)
        {
            XenoClientRecord? client = RequirePlatform().Clients.FirstOrDefault(item => item.Id == clientId && item.TenantId == tenantId);
            if (client == null) return true;
            TimeSpan age = utcNow - client.LastHeartbeatUtc;
            return age > TimeSpan.FromMilliseconds(_Settings.WebSocket.ClientStaleTimeoutMs);
        }

        private static async Task CloseStaleSessionAsync(ClientConnection connection, CancellationToken token)
        {
            try
            {
                if (connection.Session.IsConnected)
                {
                    await connection.Session.CloseAsync(WebSocketCloseStatus.EndpointUnavailable, "stale client connection", token).ConfigureAwait(false);
                }
            }
            catch (OperationCanceledException) when (token.IsCancellationRequested)
            {
            }
            catch (InvalidOperationException)
            {
            }
            catch (IOException)
            {
            }
            catch (WebSocketException)
            {
            }
        }

        private async Task TelemetryCollectionLoopAsync(CancellationToken token)
        {
            TimeSpan interval = TimeSpan.FromSeconds(_Settings.TelemetryCollection.CollectionIntervalSeconds);
            using (PeriodicTimer timer = new PeriodicTimer(interval))
            {
                _TelemetryNextCycleUtc = DateTime.UtcNow.Add(interval);
                while (!token.IsCancellationRequested)
                {
                    try
                    {
                        await timer.WaitForNextTickAsync(token).ConfigureAwait(false);
                        await CollectTelemetryForConnectedClientsAsync(null, _Settings.TelemetryCollection.CollectHostTelemetry, _Settings.TelemetryCollection.CollectProviderTelemetry, token).ConfigureAwait(false);
                        await PruneTelemetryIfDueAsync(token).ConfigureAwait(false);
                        _TelemetryLastCycleUtc = DateTime.UtcNow;
                        _TelemetryNextCycleUtc = DateTime.UtcNow.Add(interval);
                    }
                    catch (OperationCanceledException) when (token.IsCancellationRequested)
                    {
                        return;
                    }
                    catch (Exception)
                    {
                        _TelemetryRecentFailureCount++;
                    }
                }
            }
        }

        private async Task<ServerClientTelemetryCollectResponse> CollectTelemetryForConnectedClientsAsync(string? clientId, bool collectHostTelemetry, bool collectProviderTelemetry, CancellationToken token)
        {
            token.ThrowIfCancellationRequested();
            ServerClientTelemetryCollectResponse response = new ServerClientTelemetryCollectResponse();
            List<ClientTelemetryCollectionTarget> targets = SnapshotTelemetryTargets(clientId);
            response.TargetClientCount = targets.Count;

            if (Interlocked.Exchange(ref _TelemetryCollectionCycleActive, 1) == 1)
            {
                foreach (ClientTelemetryCollectionTarget target in targets)
                {
                    string attemptId = await PersistSkippedOverlapAttemptAsync(target, collectHostTelemetry, collectProviderTelemetry, token).ConfigureAwait(false);
                    response.AttemptIds.Add(attemptId);
                }

                _TelemetryRecentFailureCount++;
                return response;
            }

            try
            {
                using (SemaphoreSlim semaphore = new SemaphoreSlim(_Settings.TelemetryCollection.MaxConcurrentClientRequests))
                {
                    List<Task> tasks = new List<Task>();
                    foreach (ClientTelemetryCollectionTarget target in targets)
                    {
                        await semaphore.WaitAsync(token).ConfigureAwait(false);
                        tasks.Add(Task.Run(async () =>
                        {
                            try
                            {
                                string attemptId = await CollectTelemetryForClientAsync(target, collectHostTelemetry, collectProviderTelemetry, token).ConfigureAwait(false);
                                lock (response.AttemptIds)
                                {
                                    response.AttemptIds.Add(attemptId);
                                }
                            }
                            finally
                            {
                                semaphore.Release();
                            }
                        }, token));
                    }

                    await Task.WhenAll(tasks).ConfigureAwait(false);
                }
            }
            finally
            {
                Interlocked.Exchange(ref _TelemetryCollectionCycleActive, 0);
            }

            return response;
        }

        private async Task<string> PersistSkippedOverlapAttemptAsync(ClientTelemetryCollectionTarget target, bool collectHostTelemetry, bool collectProviderTelemetry, CancellationToken token)
        {
            ServerClientTelemetryCollectionAttemptRecord attempt = new ServerClientTelemetryCollectionAttemptRecord
            {
                TenantId = target.TenantId,
                ClientId = target.ClientId,
                ClientName = target.ClientName,
                HostRequested = collectHostTelemetry,
                ProviderRequested = collectProviderTelemetry,
                Status = "SkippedOverlap",
                ErrorCode = "SkippedOverlap",
                ErrorMessage = "A previous telemetry collection cycle is still running.",
                CompletedUtc = DateTime.UtcNow
            };
            attempt.ElapsedMs = Math.Max(0, (attempt.CompletedUtc.Value - attempt.StartedUtc).TotalMilliseconds);
            if (_Database?.ClientTelemetryCollectionAttempts != null)
            {
                await _Database.ClientTelemetryCollectionAttempts.CreateAttemptAsync(attempt, token).ConfigureAwait(false);
                await _Database.ClientTelemetryCollectionAttempts.CompleteAttemptAsync(attempt, token).ConfigureAwait(false);
            }

            return attempt.Id;
        }

        private List<ClientTelemetryCollectionTarget> SnapshotTelemetryTargets(string? clientId)
        {
            List<ClientTelemetryCollectionTarget> targets = new List<ClientTelemetryCollectionTarget>();
            lock (_ClientConnectionsLock)
            {
                foreach (ClientConnection connection in _ClientConnections.Values)
                {
                    if (!String.IsNullOrWhiteSpace(clientId) && !String.Equals(connection.ClientId, clientId, StringComparison.OrdinalIgnoreCase)) continue;
                    if (!connection.Session.IsConnected) continue;
                    XenoClientRecord? client = RequirePlatform().Clients.FirstOrDefault(item => String.Equals(item.Id, connection.ClientId, StringComparison.OrdinalIgnoreCase) && String.Equals(item.TenantId, connection.TenantId, StringComparison.OrdinalIgnoreCase));
                    if (client == null) continue;
                    targets.Add(new ClientTelemetryCollectionTarget
                    {
                        ClientId = connection.ClientId,
                        TenantId = connection.TenantId,
                        ClientName = client.Name
                    });
                }
            }

            return targets;
        }

        private ServerClientTelemetryCollectionStatusResponse BuildTelemetryStatus(RequestContext requestContext)
        {
            List<ClientTelemetryCollectionTarget> targets = SnapshotTelemetryTargets(null);
            return new ServerClientTelemetryCollectionStatusResponse
            {
                Enabled = _Settings.TelemetryCollection.Enabled,
                Running = _TelemetryCollectionTask != null && !_TelemetryCollectionTask.IsCompleted,
                CollectionIntervalSeconds = _Settings.TelemetryCollection.CollectionIntervalSeconds,
                RequestTimeoutMs = _Settings.TelemetryCollection.RequestTimeoutMs,
                RetentionDays = _Settings.TelemetryCollection.RetentionDays,
                LastCycleUtc = _TelemetryLastCycleUtc,
                NextCycleUtc = _TelemetryNextCycleUtc,
                ConnectedClientCount = RequirePlatform().Clients.Count(item => item.TenantId == requestContext.TenantId && item.Connected),
                EligibleClientCount = targets.Count(item => item.TenantId == requestContext.TenantId),
                RecentFailureCount = _TelemetryRecentFailureCount
            };
        }

        private static List<RoutingPolicyMetricDefinition> BuildTelemetryMetricCatalog()
        {
            return new List<RoutingPolicyMetricDefinition>
            {
                Metric("health.connected", "Boolean"),
                Metric("health.hasCapacity", "Boolean"),
                Metric("health.inFlightRequests", "Number"),
                Metric("client.weight", "Number"),
                Metric("client.maxParallelRequests", "Number"),
                Metric("client.heartbeatAgeMs", "Number"),
                Metric("telemetry.ageMs", "Number"),
                Metric("telemetry.host.available", "Boolean"),
                Metric("telemetry.cpu.utilizationPercent", "Number"),
                Metric("telemetry.memory.utilizationPercent", "Number"),
                Metric("telemetry.memory.availableBytes", "Number"),
                Metric("telemetry.disk.utilizationPercent", "Number"),
                Metric("telemetry.disk.readBytesPerSecond", "Number"),
                Metric("telemetry.disk.writeBytesPerSecond", "Number"),
                Metric("telemetry.gpu.available", "Boolean"),
                Metric("telemetry.gpu.deviceCount", "Number"),
                Metric("telemetry.gpu.avgUtilizationPercent", "Number"),
                Metric("telemetry.gpu.avgMemoryUtilizationPercent", "Number"),
                Metric("telemetry.gpu.totalMemoryBytes", "Number"),
                Metric("telemetry.gpu.usedMemoryBytes", "Number"),
                Metric("telemetry.gpu.avgTemperatureCelsius", "Number"),
                Metric("provider.available", "Boolean"),
                Metric("provider.healthStatus", "String"),
                Metric("provider.activeRequests", "Number"),
                Metric("provider.queuedRequests", "Number"),
                Metric("provider.waitingRequests", "Number"),
                Metric("provider.loadedModelCount", "Number"),
                Metric("provider.latencyP95Ms", "Number"),
                Metric("provider.timeToFirstTokenP95Ms", "Number"),
                Metric("provider.cacheUsagePercent", "Number"),
                Metric("provider.requestRatePerSecond", "Number"),
                Metric("provider.errorRatePerSecond", "Number")
            };
        }

        private static RoutingPolicyMetricDefinition Metric(string name, string valueType)
        {
            return new RoutingPolicyMetricDefinition
            {
                Name = name,
                ValueType = valueType
            };
        }

        private static RoutingPolicyValidationResult ValidateRoutingPolicySaveRequest(RoutingPolicySaveRequest request)
        {
            RoutingPolicyValidationResult result = new RoutingPolicyValidationResult();
            Dictionary<string, RoutingPolicyMetricDefinition> metrics = BuildTelemetryMetricCatalog()
                .ToDictionary(item => item.Name, StringComparer.OrdinalIgnoreCase);

            if (request.MaxTelemetryAgeMs.HasValue && (request.MaxTelemetryAgeMs.Value < 1000 || request.MaxTelemetryAgeMs.Value > 300000))
            {
                AddRoutingValidationIssue(result, "maxTelemetryAgeMs", "InvalidTelemetryAge", "Maximum telemetry age must be between 1000 and 300000 milliseconds.");
            }

            if (request.TelemetryFilters != null)
            {
                for (int i = 0; i < request.TelemetryFilters.Count; i++)
                {
                    RoutingPolicyCondition condition = request.TelemetryFilters[i];
                    string fieldPrefix = "telemetryFilters[" + i.ToString(CultureInfo.InvariantCulture) + "]";
                    if (!metrics.TryGetValue(condition.Metric, out RoutingPolicyMetricDefinition? metric))
                    {
                        AddRoutingValidationIssue(result, fieldPrefix + ".metric", "UnsupportedMetric", "Metric is not in the telemetry metric catalog.");
                        continue;
                    }

                    if (!String.Equals(metric.ValueType, condition.ValueType.ToString(), StringComparison.OrdinalIgnoreCase))
                    {
                        AddRoutingValidationIssue(result, fieldPrefix + ".valueType", "WrongValueType", "Condition value type does not match the metric catalog.");
                    }

                    ValidateRoutingPolicyConditionValue(result, fieldPrefix, condition);
                }
            }

            if (request.TelemetryRanking != null)
            {
                for (int i = 0; i < request.TelemetryRanking.Count; i++)
                {
                    RoutingPolicyRankingRule ranking = request.TelemetryRanking[i];
                    string fieldPrefix = "telemetryRanking[" + i.ToString(CultureInfo.InvariantCulture) + "]";
                    if (!metrics.TryGetValue(ranking.Metric, out RoutingPolicyMetricDefinition? metric))
                    {
                        AddRoutingValidationIssue(result, fieldPrefix + ".metric", "UnsupportedMetric", "Metric is not in the telemetry metric catalog.");
                    }
                    else if (!String.Equals(metric.ValueType, "Number", StringComparison.OrdinalIgnoreCase))
                    {
                        AddRoutingValidationIssue(result, fieldPrefix + ".metric", "NonNumericRankingMetric", "Ranking metrics must be numeric.");
                    }

                    if (ranking.Weight <= 0 || Double.IsNaN(ranking.Weight) || Double.IsInfinity(ranking.Weight))
                    {
                        AddRoutingValidationIssue(result, fieldPrefix + ".weight", "InvalidRankingWeight", "Ranking weight must be a positive finite number.");
                    }
                }
            }

            result.Valid = result.Issues.Count == 0;
            return result;
        }

        private static void ValidateRoutingPolicyConditionValue(RoutingPolicyValidationResult result, string fieldPrefix, RoutingPolicyCondition condition)
        {
            if (condition.ValueType == RoutingPolicyMetricValueTypeEnum.Number)
            {
                if (!Double.TryParse(condition.Value, NumberStyles.Float, CultureInfo.InvariantCulture, out double parsed) || Double.IsNaN(parsed) || Double.IsInfinity(parsed))
                {
                    AddRoutingValidationIssue(result, fieldPrefix + ".value", "InvalidNumberValue", "Condition value must be a finite number.");
                }

                return;
            }

            if (condition.ValueType == RoutingPolicyMetricValueTypeEnum.Boolean)
            {
                if (!Boolean.TryParse(condition.Value, out bool _))
                {
                    AddRoutingValidationIssue(result, fieldPrefix + ".value", "InvalidBooleanValue", "Condition value must be true or false.");
                }

                return;
            }

            if (String.IsNullOrWhiteSpace(condition.Value))
            {
                AddRoutingValidationIssue(result, fieldPrefix + ".value", "InvalidStringValue", "Condition value must not be empty.");
            }
        }

        private static void AddRoutingValidationIssue(RoutingPolicyValidationResult result, string field, string code, string message)
        {
            result.Issues.Add(new RoutingPolicyValidationIssue
            {
                Field = field,
                Code = code,
                Message = message
            });
        }

        private async Task<string> CollectTelemetryForClientAsync(ClientTelemetryCollectionTarget target, bool collectHostTelemetry, bool collectProviderTelemetry, CancellationToken token)
        {
            token.ThrowIfCancellationRequested();
            ClientConnection? connection = GetClientConnection(target.ClientId);
            ServerClientTelemetryCollectionAttemptRecord attempt = new ServerClientTelemetryCollectionAttemptRecord
            {
                TenantId = target.TenantId,
                ClientId = target.ClientId,
                ClientName = target.ClientName,
                HostRequested = collectHostTelemetry,
                ProviderRequested = collectProviderTelemetry,
                Status = "Started"
            };

            if (_Database?.ClientTelemetryCollectionAttempts != null)
            {
                await _Database.ClientTelemetryCollectionAttempts.CreateAttemptAsync(attempt, token).ConfigureAwait(false);
            }

            if (connection == null || !connection.Session.IsConnected)
            {
                attempt.Status = "Disconnected";
                attempt.CompletedUtc = DateTime.UtcNow;
                attempt.ElapsedMs = Math.Max(0, (attempt.CompletedUtc.Value - attempt.StartedUtc).TotalMilliseconds);
                if (_Database?.ClientTelemetryCollectionAttempts != null) await _Database.ClientTelemetryCollectionAttempts.CompleteAttemptAsync(attempt, token).ConfigureAwait(false);
                return attempt.Id;
            }

            PendingTelemetryResponse pending = new PendingTelemetryResponse();
            lock (_PendingTelemetryResponsesLock)
            {
                _PendingTelemetryResponses[attempt.Id] = pending;
            }

            try
            {
                ServerTelemetryCollectionRequestMessage request = new ServerTelemetryCollectionRequestMessage
                {
                    RequestId = attempt.Id,
                    TenantId = target.TenantId,
                    ClientId = target.ClientId,
                    CollectHostTelemetry = collectHostTelemetry,
                    CollectProviderTelemetry = collectProviderTelemetry,
                    IncludeRawSnapshot = _Settings.TelemetryCollection.PersistRawSnapshots,
                    MaxRawSnapshotBytes = _Settings.TelemetryCollection.MaxRawSnapshotBytes,
                    MaxProviderMetricRows = _Settings.TelemetryCollection.MaxProviderMetricRowsPerResponse,
                    MaxSamples = _Settings.TelemetryCollection.MaxSamplesPerClientResponse
                };
                await SendToClientAsync(connection, JsonUtil.Serialize(request), token).ConfigureAwait(false);

                if (request.CollectHostTelemetry && _Database?.ClientTelemetry != null)
                {
                    ServerClientTelemetrySampleRecord? record = await WaitForHostTelemetryAsync(pending, token).ConfigureAwait(false);
                    if (record != null)
                    {
                        attempt.HostSucceeded = true;
                        attempt.HostSampleId = record.Id;
                    }
                }

                if (request.CollectProviderTelemetry && _Database?.ClientProviderTelemetry != null)
                {
                    IReadOnlyList<ServerProviderTelemetrySampleRecord> records = await WaitForProviderTelemetryAsync(pending, token).ConfigureAwait(false);
                    if (records.Count > 0)
                    {
                        attempt.ProviderSucceeded = true;
                        attempt.ProviderSampleCount = records.Count;
                    }
                }

                attempt.Status = "Completed";
            }
            catch (OperationCanceledException)
            {
                attempt.Status = "TimedOut";
                attempt.ErrorCode = "TelemetryTimeout";
                attempt.ErrorMessage = "Client telemetry collection timed out.";
                _TelemetryRecentFailureCount++;
            }
            catch (Exception ex) when (ex is InvalidOperationException || ex is UnauthorizedAccessException || ex is ArgumentException)
            {
                attempt.Status = "Failed";
                attempt.ErrorCode = ex.GetType().Name;
                attempt.ErrorMessage = ex.Message;
                _TelemetryRecentFailureCount++;
            }
            finally
            {
                lock (_PendingTelemetryResponsesLock)
                {
                    _PendingTelemetryResponses.Remove(attempt.Id);
                }

                attempt.CompletedUtc = DateTime.UtcNow;
                attempt.ElapsedMs = Math.Max(0, (attempt.CompletedUtc.Value - attempt.StartedUtc).TotalMilliseconds);
                if (_Database?.ClientTelemetryCollectionAttempts != null)
                {
                    await _Database.ClientTelemetryCollectionAttempts.CompleteAttemptAsync(attempt, CancellationToken.None).ConfigureAwait(false);
                }
            }

            return attempt.Id;
        }

        private async Task<ServerClientTelemetrySampleRecord?> WaitForHostTelemetryAsync(PendingTelemetryResponse pending, CancellationToken token)
        {
            using (CancellationTokenSource timeout = CancellationTokenSource.CreateLinkedTokenSource(token))
            {
                timeout.CancelAfter(TimeSpan.FromMilliseconds(_Settings.TelemetryCollection.RequestTimeoutMs));
                Task delay = Task.Delay(Timeout.InfiniteTimeSpan, timeout.Token);
                Task completed = await Task.WhenAny(pending.Host.Task, pending.HostError.Task, delay).ConfigureAwait(false);
                if (completed == pending.Host.Task) return await pending.Host.Task.ConfigureAwait(false);
                if (completed == pending.HostError.Task)
                {
                    ClientTelemetryErrorMessage error = await pending.HostError.Task.ConfigureAwait(false);
                    throw new InvalidOperationException(error.ErrorMessage);
                }

                throw new OperationCanceledException(timeout.Token);
            }
        }

        private async Task<IReadOnlyList<ServerProviderTelemetrySampleRecord>> WaitForProviderTelemetryAsync(PendingTelemetryResponse pending, CancellationToken token)
        {
            using (CancellationTokenSource timeout = CancellationTokenSource.CreateLinkedTokenSource(token))
            {
                timeout.CancelAfter(TimeSpan.FromMilliseconds(_Settings.TelemetryCollection.RequestTimeoutMs));
                Task delay = Task.Delay(Timeout.InfiniteTimeSpan, timeout.Token);
                Task completed = await Task.WhenAny(pending.Provider.Task, pending.ProviderError.Task, delay).ConfigureAwait(false);
                if (completed == pending.Provider.Task) return await pending.Provider.Task.ConfigureAwait(false);
                if (completed == pending.ProviderError.Task)
                {
                    ClientTelemetryErrorMessage error = await pending.ProviderError.Task.ConfigureAwait(false);
                    throw new InvalidOperationException(error.ErrorMessage);
                }

                throw new OperationCanceledException(timeout.Token);
            }
        }

        private string? LimitRawSnapshot(string json)
        {
            if (_Settings.TelemetryCollection.MaxRawSnapshotBytes == 0) return null;
            byte[] bytes = Encoding.UTF8.GetBytes(json);
            if (bytes.Length <= _Settings.TelemetryCollection.MaxRawSnapshotBytes) return json;
            return Encoding.UTF8.GetString(bytes, 0, _Settings.TelemetryCollection.MaxRawSnapshotBytes);
        }

        private async Task PruneTelemetryIfDueAsync(CancellationToken token)
        {
            DateTime now = DateTime.UtcNow;
            if (_TelemetryNextPruneUtc.HasValue && now < _TelemetryNextPruneUtc.Value) return;
            _TelemetryNextPruneUtc = now.AddMinutes(_Settings.TelemetryCollection.PruneIntervalMinutes);

            DateTime olderThanUtc = now.AddDays(-_Settings.TelemetryCollection.RetentionDays);
            if (_Database?.ClientTelemetry != null)
            {
                await _Database.ClientTelemetry.DeleteOlderThanAsync(olderThanUtc, token).ConfigureAwait(false);
            }

            if (_Database?.ClientProviderTelemetry != null)
            {
                await _Database.ClientProviderTelemetry.DeleteOlderThanAsync(olderThanUtc, token).ConfigureAwait(false);
            }
        }

        private async Task<ClientResponseMessage> WaitForClientFirstResponseAsync(string requestId, PendingClientResponse pending, CancellationToken token)
        {
            using (CancellationTokenSource timeout = CancellationTokenSource.CreateLinkedTokenSource(token))
            {
                timeout.CancelAfter(TimeSpan.FromMilliseconds(Math.Max(1000, _Settings.WebSocket.RequestTimeoutMs)));
                using (timeout.Token.Register(delegate { pending.Cancel(timeout.Token); }))
                {
                    Task<ClientResponseMessage> completed = await Task.WhenAny(pending.Start.Task, pending.Final.Task).ConfigureAwait(false);
                    return await completed.ConfigureAwait(false);
                }
            }
        }

        private async Task<ClientResponseMessage> WaitForClientResponseAsync(string requestId, PendingClientResponse pending, CancellationToken token)
        {
            using (CancellationTokenSource timeout = CancellationTokenSource.CreateLinkedTokenSource(token))
            {
                timeout.CancelAfter(TimeSpan.FromMilliseconds(Math.Max(1000, _Settings.WebSocket.RequestTimeoutMs)));
                using (timeout.Token.Register(delegate { pending.Cancel(timeout.Token); }))
                {
                    return await pending.Final.Task.ConfigureAwait(false);
                }
            }
        }

        private async Task WebSocketRouteAsync(HttpContextBase context, WebSocketSession session)
        {
            string? registeredClientId = null;
            RequestContext requestContext;
            try
            {
                requestContext = CreateWebSocketRequestContext(context);
            }
            catch (UnauthorizedAccessException ex)
            {
                await session.SendTextAsync(JsonUtil.Serialize(new
                {
                    type = "server.unauthorized",
                    message = ex.Message
                }), context.Token).ConfigureAwait(false);
                await session.CloseAsync(WebSocketCloseStatus.PolicyViolation, "unauthorized", context.Token).ConfigureAwait(false);
                return;
            }
            catch (ArgumentException ex)
            {
                await session.SendTextAsync(JsonUtil.Serialize(new
                {
                    type = "server.invalid-auth",
                    message = ex.Message
                }), context.Token).ConfigureAwait(false);
                await session.CloseAsync(WebSocketCloseStatus.PolicyViolation, "invalid authentication", context.Token).ConfigureAwait(false);
                return;
            }

            await session.SendTextAsync(JsonUtil.Serialize(new
            {
                type = "server.connected",
                sessionId = session.Id.ToString(),
                tenantId = requestContext.TenantId,
                heartbeatIntervalMs = _Settings.WebSocket.HeartbeatIntervalMs
            }), context.Token).ConfigureAwait(false);

            try
            {
                while (session.IsConnected && !context.Token.IsCancellationRequested)
                {
                    WebSocketMessage message = await session.ReceiveAsync(context.Token).ConfigureAwait(false);
                    if (message.MessageType == WebSocketMessageType.Close) break;

                    string text = message.Text ?? String.Empty;
                    string? responseType;
                    try
                    {
                        responseType = await HandleClientMessageAsync(session, text, requestContext, delegate(string clientId) { registeredClientId = clientId; }, context.Token).ConfigureAwait(false);
                    }
                    catch (InvalidDataException)
                    {
                        responseType = "server.invalid-json";
                    }
                    catch (UnauthorizedAccessException)
                    {
                        responseType = "server.unauthorized";
                    }
                    catch (ArgumentException)
                    {
                        responseType = "server.invalid-message";
                    }

                    if (String.IsNullOrWhiteSpace(responseType)) continue;
                    await session.SendTextAsync(JsonUtil.Serialize(new
                    {
                        type = responseType,
                        receivedUtc = DateTime.UtcNow,
                        heartbeatIntervalMs = _Settings.WebSocket.HeartbeatIntervalMs
                    }), context.Token).ConfigureAwait(false);
                }
            }
            finally
            {
                if (!String.IsNullOrWhiteSpace(registeredClientId))
                {
                    await UnregisterClientConnectionAsync(registeredClientId, session, CancellationToken.None).ConfigureAwait(false);
                }
            }

            await session.CloseAsync(WebSocketCloseStatus.NormalClosure, "closing", context.Token).ConfigureAwait(false);
        }

        private async Task<string?> HandleClientMessageAsync(WebSocketSession session, string json, RequestContext requestContext, Action<string> registeredClientCallback, CancellationToken token)
        {
            if (String.IsNullOrWhiteSpace(json)) return "server.empty";

            WebSocketMessageEnvelope? envelope = JsonUtil.Deserialize<WebSocketMessageEnvelope>(json);
            string type = envelope?.Type ?? String.Empty;
            if (String.Equals(type, "client.register", StringComparison.OrdinalIgnoreCase))
            {
                ClientRegisterMessage? message = JsonUtil.Deserialize<ClientRegisterMessage>(json);
                if (message == null) throw new ArgumentException("Client registration message is invalid.", nameof(json));
                string clientId = RegisterClient(message, session, requestContext);
                registeredClientCallback(clientId);
                await SavePlatformAsync(token).ConfigureAwait(false);
                return "server.registered";
            }

            if (String.Equals(type, "client.heartbeat", StringComparison.OrdinalIgnoreCase))
            {
                ClientHeartbeatMessage? message = JsonUtil.Deserialize<ClientHeartbeatMessage>(json);
                UpdateClientHeartbeat(message?.ClientId, session, requestContext);
                await SavePlatformAsync(token).ConfigureAwait(false);
                return "server.heartbeat";
            }

            if (String.Equals(type, "client.inventory.update", StringComparison.OrdinalIgnoreCase))
            {
                ClientInventoryUpdateMessage? message = JsonUtil.Deserialize<ClientInventoryUpdateMessage>(json);
                UpdateClientInventory(message, requestContext);
                await SavePlatformAsync(token).ConfigureAwait(false);
                return "server.inventory.updated";
            }

            if (String.Equals(type, "client.telemetry.snapshot", StringComparison.OrdinalIgnoreCase))
            {
                ClientTelemetrySnapshotMessage? message = JsonUtil.Deserialize<ClientTelemetrySnapshotMessage>(json);
                if (message != null) await AcceptClientTelemetrySnapshotAsync(session, message, requestContext, token).ConfigureAwait(false);
                return null;
            }

            if (String.Equals(type, "client.providerTelemetry.snapshot", StringComparison.OrdinalIgnoreCase))
            {
                ClientProviderTelemetrySnapshotMessage? message = JsonUtil.Deserialize<ClientProviderTelemetrySnapshotMessage>(json);
                if (message != null) await AcceptClientProviderTelemetrySnapshotAsync(session, message, requestContext, token).ConfigureAwait(false);
                return null;
            }

            if (String.Equals(type, "client.telemetry.error", StringComparison.OrdinalIgnoreCase) ||
                String.Equals(type, "client.providerTelemetry.error", StringComparison.OrdinalIgnoreCase))
            {
                ClientTelemetryErrorMessage? message = JsonUtil.Deserialize<ClientTelemetryErrorMessage>(json);
                if (message != null) await AcceptClientTelemetryErrorAsync(session, message, requestContext, token).ConfigureAwait(false);
                return null;
            }

            if (String.Equals(type, "client.response.start", StringComparison.OrdinalIgnoreCase))
            {
                ClientResponseMessage? message = JsonUtil.Deserialize<ClientResponseMessage>(json);
                if (message != null) StartClientResponse(message);
                return null;
            }

            if (String.Equals(type, "client.response.chunk", StringComparison.OrdinalIgnoreCase))
            {
                ClientResponseChunkMessage? message = JsonUtil.Deserialize<ClientResponseChunkMessage>(json);
                if (message != null) AddClientResponseChunk(message);
                return null;
            }

            if (String.Equals(type, "client.response.complete", StringComparison.OrdinalIgnoreCase) ||
                String.Equals(type, "client.response", StringComparison.OrdinalIgnoreCase))
            {
                ClientResponseMessage? message = JsonUtil.Deserialize<ClientResponseMessage>(json);
                if (message != null) CompleteClientResponse(message);
                return null;
            }

            return "server.ack";
        }

        private async Task AcceptClientTelemetrySnapshotAsync(WebSocketSession session, ClientTelemetrySnapshotMessage message, RequestContext requestContext, CancellationToken token)
        {
            ValidateTelemetryFrame(session, message.RequestId, message.TenantId, message.ClientId, requestContext);
            PendingTelemetryResponse? pending = GetPendingTelemetryResponse(message.RequestId);
            ServerClientTelemetrySampleRecord? record = null;
            if (_Database?.ClientTelemetry != null)
            {
                string? rawJson = _Settings.TelemetryCollection.PersistRawSnapshots ? LimitRawSnapshot(JsonUtil.Serialize(message.Snapshot, true)) : null;
                record = await _Database.ClientTelemetry.CreateHostSampleAsync(
                    requestContext.TenantId,
                    message.ClientId,
                    TelemetryClientName(requestContext.TenantId, message.ClientId),
                    message.RequestId,
                    message.Snapshot,
                    rawJson,
                    token).ConfigureAwait(false);
                _TelemetryLatestCache.SetHost(record);
            }

            pending?.SetHost(record);
            await SendTelemetryAckAsync(message.RequestId, message.TenantId, message.ClientId, true, null, token).ConfigureAwait(false);
        }

        private async Task AcceptClientProviderTelemetrySnapshotAsync(WebSocketSession session, ClientProviderTelemetrySnapshotMessage message, RequestContext requestContext, CancellationToken token)
        {
            ValidateTelemetryFrame(session, message.RequestId, message.TenantId, message.ClientId, requestContext);
            PendingTelemetryResponse? pending = GetPendingTelemetryResponse(message.RequestId);
            IReadOnlyList<ServerProviderTelemetrySampleRecord> records = Array.Empty<ServerProviderTelemetrySampleRecord>();
            if (_Database?.ClientProviderTelemetry != null)
            {
                records = await _Database.ClientProviderTelemetry.CreateProviderSnapshotAsync(
                    requestContext.TenantId,
                    message.ClientId,
                    TelemetryClientName(requestContext.TenantId, message.ClientId),
                    message.RequestId,
                    message.Snapshot,
                    _Settings.TelemetryCollection.PersistRawSnapshots,
                    token).ConfigureAwait(false);
                foreach (ServerProviderTelemetrySampleRecord record in records)
                {
                    _TelemetryLatestCache.SetProvider(record);
                }
            }

            pending?.SetProvider(records);
            await SendTelemetryAckAsync(message.RequestId, message.TenantId, message.ClientId, true, null, token).ConfigureAwait(false);
        }

        private async Task AcceptClientTelemetryErrorAsync(WebSocketSession session, ClientTelemetryErrorMessage message, RequestContext requestContext, CancellationToken token)
        {
            ValidateTelemetryFrame(session, message.RequestId, message.TenantId, message.ClientId, requestContext);
            PendingTelemetryResponse? pending = GetPendingTelemetryResponse(message.RequestId);
            pending?.SetError(message);
            await SendTelemetryAckAsync(message.RequestId, message.TenantId, message.ClientId, false, message.ErrorMessage, token).ConfigureAwait(false);
        }

        private PendingTelemetryResponse? GetPendingTelemetryResponse(string requestId)
        {
            if (String.IsNullOrWhiteSpace(requestId)) return null;
            lock (_PendingTelemetryResponsesLock)
            {
                _PendingTelemetryResponses.TryGetValue(requestId, out PendingTelemetryResponse? pending);
                return pending;
            }
        }

        private void ValidateTelemetryFrame(WebSocketSession session, string requestId, string tenantId, string clientId, RequestContext requestContext)
        {
            if (String.IsNullOrWhiteSpace(requestId)) throw new ArgumentException("Telemetry frame request ID is required.");
            if (!String.Equals(tenantId, requestContext.TenantId, StringComparison.OrdinalIgnoreCase)) throw new UnauthorizedAccessException("Telemetry frame tenant does not match authenticated tenant.");
            ClientConnection? connection = GetClientConnection(clientId);
            if (connection == null) throw new UnauthorizedAccessException("Telemetry frame client is not registered.");
            if (!String.Equals(connection.TenantId, tenantId, StringComparison.OrdinalIgnoreCase)) throw new UnauthorizedAccessException("Telemetry frame client tenant is invalid.");
            if (!String.Equals(connection.Session.Id.ToString(), session.Id.ToString(), StringComparison.OrdinalIgnoreCase)) throw new UnauthorizedAccessException("Telemetry frame arrived on the wrong client session.");
        }

        private string? TelemetryClientName(string tenantId, string clientId)
        {
            XenoClientRecord? client = RequirePlatform().Clients.FirstOrDefault(item =>
                String.Equals(item.Id, clientId, StringComparison.OrdinalIgnoreCase)
                && String.Equals(item.TenantId, tenantId, StringComparison.OrdinalIgnoreCase));
            return client?.Name;
        }

        private async Task SendTelemetryAckAsync(string requestId, string tenantId, string clientId, bool accepted, string? message, CancellationToken token)
        {
            ClientConnection? connection = GetClientConnection(clientId);
            if (connection == null || !connection.Session.IsConnected) return;
            await SendToClientAsync(connection, JsonUtil.Serialize(new ServerTelemetryAckMessage
            {
                RequestId = requestId,
                TenantId = tenantId,
                ClientId = clientId,
                Accepted = accepted,
                Message = message
            }), token).ConfigureAwait(false);
        }

        private string RegisterClient(ClientRegisterMessage message, WebSocketSession session, RequestContext requestContext)
        {
            XenoPlatform platform = RequirePlatform();
            string tenantId = message.TenantId ?? requestContext.TenantId;
            if (!String.Equals(tenantId, requestContext.TenantId, StringComparison.OrdinalIgnoreCase))
            {
                throw new UnauthorizedAccessException("Client tenant does not match authenticated tenant.");
            }

            string clientId = message.ClientId ?? IdGenerator.NewId("xcl_");
            string name = message.Name ?? clientId;

            XenoClientRecord? client = platform.Clients.FirstOrDefault(item => item.Id == clientId);
            if (client != null && !String.Equals(client.TenantId, tenantId, StringComparison.OrdinalIgnoreCase))
            {
                throw new UnauthorizedAccessException("Client ID belongs to a different tenant.");
            }

            ClientConnection? staleConnection = null;
            lock (_ClientConnectionsLock)
            {
                if (_ClientConnections.TryGetValue(clientId, out ClientConnection? existingConnection) &&
                    existingConnection.Session.IsConnected &&
                    !String.Equals(existingConnection.Session.Id.ToString(), session.Id.ToString(), StringComparison.OrdinalIgnoreCase))
                {
                    if (!IsClientConnectionStale(clientId, tenantId, DateTime.UtcNow))
                    {
                        throw new UnauthorizedAccessException("Client ID is already connected.");
                    }

                    _ClientConnections.Remove(clientId);
                    staleConnection = existingConnection;
                }
            }

            if (staleConnection != null) _ = CloseStaleSessionAsync(staleConnection, CancellationToken.None);

            if (client == null)
            {
                client = new XenoClientRecord
                {
                    Id = clientId,
                    TenantId = tenantId,
                    Name = name
                };
                platform.Clients.Add(client);
            }

            BindClientOwner(client, requestContext);
            client.TenantId = tenantId;
            client.Name = name;
            client.Connected = true;
            client.LastHeartbeatUtc = DateTime.UtcNow;
            lock (_ClientConnectionsLock)
            {
                _ClientConnections[clientId] = new ClientConnection(clientId, tenantId, session);
            }

            if (message.Inventory != null) RegisterClientInventory(platform, client, message.Inventory);

            return clientId;
        }

        private void UpdateClientInventory(ClientInventoryUpdateMessage? message, RequestContext requestContext)
        {
            if (message == null || String.IsNullOrWhiteSpace(message.ClientId)) throw new ArgumentException("Client inventory update message is invalid.");
            XenoPlatform platform = RequirePlatform();
            string tenantId = message.TenantId ?? requestContext.TenantId;
            if (!String.Equals(tenantId, requestContext.TenantId, StringComparison.OrdinalIgnoreCase))
            {
                throw new UnauthorizedAccessException("Client tenant does not match authenticated tenant.");
            }

            XenoClientRecord client = platform.Clients.FirstOrDefault(item => String.Equals(item.Id, message.ClientId, StringComparison.OrdinalIgnoreCase) && String.Equals(item.TenantId, tenantId, StringComparison.OrdinalIgnoreCase))
                ?? throw new ArgumentException("Client is not registered.");

            client.LastHeartbeatUtc = DateTime.UtcNow;
            BindClientOwner(client, requestContext);
            if (message.Inventory != null) RegisterClientInventory(platform, client, message.Inventory);
        }

        private static void RegisterClientInventory(XenoPlatform platform, XenoClientRecord client, ClientInventoryMessage inventory)
        {
            client.ServiceState = ClientServiceStateEnum.Normal;
            client.Models.Clear();

            if (inventory.Models.Count > 0)
            {
                RegisterClientModels(platform, client, "client", ApiProviderTypeEnum.OpenAICompatible, inventory.Models);
            }

            if (inventory.Providers.Count > 0)
            {
                RegisterClientSources(platform, client, inventory.Providers);
            }

            if (inventory.Runners.Count > 0)
            {
                RegisterClientSources(platform, client, inventory.Runners);
            }
        }

        private static void RegisterClientSources(XenoPlatform platform, XenoClientRecord client, List<ClientInventorySourceMessage> sources)
        {
            foreach (ClientInventorySourceMessage source in sources)
            {
                string sourceName = String.IsNullOrWhiteSpace(source.Name) ? "local" : source.Name;
                string providerName = String.IsNullOrWhiteSpace(source.ApiType) ? "Ollama" : source.ApiType;
                ApiProviderTypeEnum provider = Enum.TryParse(providerName, true, out ApiProviderTypeEnum parsedProvider) ? parsedProvider : ApiProviderTypeEnum.OpenAICompatible;
                if (source.Models.Count > 0)
                {
                    RegisterClientModels(platform, client, sourceName, provider, source.Models);
                }
            }
        }

        private static void RegisterClientModels(XenoPlatform platform, XenoClientRecord client, string sourceName, ApiProviderTypeEnum provider, List<ClientInventoryModelMessage> models)
        {
            foreach (ClientInventoryModelMessage model in models)
            {
                string? modelName = model.Name;
                if (String.IsNullOrWhiteSpace(modelName)) continue;

                bool supportsCompletions = model.SupportsCompletions;
                bool supportsEmbeddings = model.SupportsEmbeddings;
                client.Models.Add(new XenoClientModel
                {
                    Name = modelName,
                    Provider = provider,
                    Source = sourceName,
                    SupportsCompletions = supportsCompletions,
                    SupportsEmbeddings = supportsEmbeddings,
                    LastAdvertisedUtc = DateTime.UtcNow
                });

                ModelDefinition? definition = platform.ModelDefinitions.FirstOrDefault(item =>
                    item.TenantId == client.TenantId &&
                    String.Equals(item.Name, modelName, StringComparison.OrdinalIgnoreCase));

                if (definition == null)
                {
                    definition = new ModelDefinition
                    {
                        TenantId = client.TenantId,
                        Name = modelName
                    };
                    platform.ModelDefinitions.Add(definition);
                }

                definition.SupportsCompletions = supportsCompletions;
                definition.SupportsEmbeddings = supportsEmbeddings;
            }
        }

        private void UpdateClientHeartbeat(string? clientId, WebSocketSession session, RequestContext requestContext)
        {
            if (String.IsNullOrWhiteSpace(clientId)) return;
            ClientConnection? connection = GetClientConnection(clientId);
            if (connection == null) throw new UnauthorizedAccessException("Client heartbeat arrived before registration.");
            if (!String.Equals(connection.TenantId, requestContext.TenantId, StringComparison.OrdinalIgnoreCase)) throw new UnauthorizedAccessException("Client heartbeat tenant is invalid.");
            if (!String.Equals(connection.Session.Id.ToString(), session.Id.ToString(), StringComparison.OrdinalIgnoreCase)) throw new UnauthorizedAccessException("Client heartbeat arrived on the wrong session.");
            XenoClientRecord? client = RequirePlatform().Clients.FirstOrDefault(item => item.Id == clientId && item.TenantId == requestContext.TenantId);
            if (client == null) return;
            BindClientOwner(client, requestContext);
            client.Connected = true;
            client.LastHeartbeatUtc = DateTime.UtcNow;
        }

        private static void BindClientOwner(XenoClientRecord client, RequestContext requestContext)
        {
            if (client == null) throw new ArgumentNullException(nameof(client));
            if (requestContext == null) throw new ArgumentNullException(nameof(requestContext));
            if (!requestContext.IsAuthenticated || String.IsNullOrWhiteSpace(requestContext.UserId))
            {
                throw new UnauthorizedAccessException("Client ownership requires an authenticated user context.");
            }

            if (!String.IsNullOrWhiteSpace(client.OwnerUserId) &&
                !String.Equals(client.OwnerUserId, requestContext.UserId, StringComparison.OrdinalIgnoreCase))
            {
                throw new UnauthorizedAccessException("Client ID is already bound to a different user.");
            }

            client.OwnerUserId = requestContext.UserId;
            client.OwnerCredentialId = requestContext.CredentialId;
            client.OwnerPrincipalType = requestContext.PrincipalType;
        }

        private void StartClientResponse(ClientResponseMessage message)
        {
            string? requestId = message.RequestId;
            if (String.IsNullOrWhiteSpace(requestId)) return;

            PendingClientResponse? pending = null;
            lock (_PendingResponsesLock)
            {
                _PendingClientResponses.TryGetValue(requestId, out pending);
            }

            pending?.SetStart(message);
        }

        private void AddClientResponseChunk(ClientResponseChunkMessage message)
        {
            string? requestId = message.RequestId;
            if (String.IsNullOrWhiteSpace(requestId)) return;

            PendingClientResponse? pending = null;
            lock (_PendingResponsesLock)
            {
                _PendingClientResponses.TryGetValue(requestId, out pending);
            }

            pending?.AddChunk(message);
        }

        private void CompleteClientResponse(ClientResponseMessage message)
        {
            string? requestId = message.RequestId;
            if (String.IsNullOrWhiteSpace(requestId)) return;

            PendingClientResponse? pending = null;
            lock (_PendingResponsesLock)
            {
                _PendingClientResponses.TryGetValue(requestId, out pending);
            }

            pending?.SetFinal(message);
        }

        private ClientConnection? GetClientConnection(string? clientId)
        {
            if (String.IsNullOrWhiteSpace(clientId)) return null;
            lock (_ClientConnectionsLock)
            {
                _ClientConnections.TryGetValue(clientId, out ClientConnection? connection);
                return connection;
            }
        }

        private async Task UnregisterClientConnectionAsync(string clientId, WebSocketSession session, CancellationToken token)
        {
            bool disconnected = false;
            lock (_ClientConnectionsLock)
            {
                if (_ClientConnections.TryGetValue(clientId, out ClientConnection? connection) && connection.Session.Id == session.Id)
                {
                    _ClientConnections.Remove(clientId);
                    disconnected = true;
                }
            }

            if (!disconnected) return;
            XenoPlatform platform = RequirePlatform();
            XenoClientRecord? client = platform.Clients.FirstOrDefault(item => item.Id == clientId);
            if (client != null)
            {
                client.Connected = false;
                client.InFlightRequests = 0;
                client.ServiceState = ClientServiceStateEnum.Quarantined;
            }

            await SavePlatformAsync(token).ConfigureAwait(false);
        }

        private void MarkClientsDisconnected()
        {
            XenoPlatform platform = RequirePlatform();
            foreach (XenoClientRecord client in platform.Clients)
            {
                client.Connected = false;
                client.InFlightRequests = 0;
                client.ServiceState = ClientServiceStateEnum.Quarantined;
            }
        }

        private async Task<RequestHistoryEntry> RecordAndSaveHistoryAsync(
            RequestContext requestContext,
            string method,
            string path,
            RoutingDecision decision,
            CancellationToken token,
            Action<RequestHistoryEntry>? configure = null)
        {
            RequestHistoryEntry entry = RequireRouting().RecordHistory(requestContext, path, method, decision);
            configure?.Invoke(entry);
            TryEnqueueLedgerEvent(entry);
            await SavePlatformAsync(token).ConfigureAwait(false);
            return entry;
        }

        private static bool IsClientResponseStart(ClientResponseMessage message)
        {
            return String.Equals(message.Type, "client.response.start", StringComparison.OrdinalIgnoreCase);
        }

        private void TryEnqueueLedgerEvent(RequestHistoryEntry entry)
        {
            if (_Ledger == null) return;
            try
            {
                _Ledger.TryEnqueueFromHistory(entry, out string? _);
            }
            catch (ArgumentException)
            {
            }
            catch (InvalidOperationException)
            {
            }
        }

        private static void PopulateRequestHistory(
            RequestHistoryEntry entry,
            RequestContext requestContext,
            string requestBodyJson,
            Dictionary<string, string> requestHeaders,
            string? responseBody,
            Dictionary<string, string> responseHeaders,
            ClientResponseMessage response,
            RoutingDecision decision)
        {
            DateTime completedUtc = DateTime.UtcNow;
            entry.UserId = requestContext.UserId;
            entry.CredentialId = requestContext.CredentialId;
            entry.SessionId = requestContext.SessionId;
            entry.PrincipalType = requestContext.PrincipalType;
            entry.RequestorTenantId = requestContext.TenantId;
            entry.RequestorPrincipalType = requestContext.PrincipalType;
            entry.RequestorPrincipalId = requestContext.PrincipalId;
            entry.AuthScheme = requestContext.AuthScheme;
            entry.SourceIp = requestContext.SourceIp;
            entry.CompletedUtc = completedUtc;
            entry.DurationMs = Math.Max(0, (long)(completedUtc - requestContext.StartedUtc).TotalMilliseconds);
            entry.RequestHeaders = RedactHeaders(requestHeaders);
            entry.ResponseHeaders = RedactHeaders(responseHeaders);
            entry.RequestBodyLength = Encoding.UTF8.GetByteCount(requestBodyJson ?? String.Empty);
            entry.ResponseBodyLength = Encoding.UTF8.GetByteCount(responseBody ?? String.Empty);
            entry.RequestBody = TruncateBody(RedactSensitiveJsonBody(requestBodyJson), out bool requestTruncated);
            entry.ResponseBody = TruncateBody(RedactSensitiveJsonBody(responseBody), out bool responseTruncated);
            entry.RequestBodyTruncated = requestTruncated;
            entry.ResponseBodyTruncated = responseTruncated;
            entry.Telemetry = ExtractTelemetry(response, decision, entry);
            entry.Timeline = BuildTimeline(requestContext, decision, entry.Telemetry, completedUtc);
        }

        private static RequestHistoryTelemetry ExtractTelemetry(ClientResponseMessage response, RoutingDecision decision, RequestHistoryEntry entry)
        {
            RequestHistoryTelemetry telemetry = new RequestHistoryTelemetry
            {
                TotalTimeMs = entry.DurationMs,
                ClientRoundTripMs = decision.ClientRoundTripMs,
                RequestBytes = entry.RequestBodyLength,
                ResponseBytes = entry.ResponseBodyLength
            };

            if (response.Telemetry != null)
            {
                RequestHistoryTelemetry source = response.Telemetry;
                telemetry.TotalTimeMs = source.TotalTimeMs ?? telemetry.TotalTimeMs;
                telemetry.ClientRoundTripMs = source.ClientRoundTripMs ?? telemetry.ClientRoundTripMs;
                telemetry.UpstreamTotalMs = source.UpstreamTotalMs ?? telemetry.UpstreamTotalMs;
                telemetry.RequestToHeadersMs = source.RequestToHeadersMs ?? telemetry.RequestToHeadersMs;
                telemetry.TimeToFirstTokenMs = source.TimeToFirstTokenMs ?? telemetry.TimeToFirstTokenMs;
                telemetry.TotalStreamingTimeMs = source.TotalStreamingTimeMs ?? telemetry.TotalStreamingTimeMs;
                telemetry.PromptTokens = source.PromptTokens ?? telemetry.PromptTokens;
                telemetry.CompletionTokens = source.CompletionTokens ?? telemetry.CompletionTokens;
                telemetry.TotalTokens = source.TotalTokens ?? telemetry.TotalTokens;
                telemetry.StreamChunkCount = source.StreamChunkCount ?? telemetry.StreamChunkCount;
                telemetry.StreamEventCount = source.StreamEventCount ?? telemetry.StreamEventCount;
                telemetry.RequestBytes = source.RequestBytes ?? telemetry.RequestBytes;
                telemetry.ResponseBytes = source.ResponseBytes ?? telemetry.ResponseBytes;
                telemetry.TransferMode = source.TransferMode == RequestTransferModeEnum.Unknown ? telemetry.TransferMode : source.TransferMode;
                telemetry.ProviderRequestId = source.ProviderRequestId ?? telemetry.ProviderRequestId;
                telemetry.Metadata = new Dictionary<string, string>(source.Metadata, StringComparer.OrdinalIgnoreCase);
            }

            if (!telemetry.TotalTokens.HasValue)
            {
                OpenAiUsage usage = ExtractUsage(response.Body);
                telemetry.TotalTokens = usage.TotalTokens;
            }

            return telemetry;
        }

        private static OpenAiUsage ExtractUsage(string? body)
        {
            if (String.IsNullOrWhiteSpace(body)) return new OpenAiUsage();
            if (!JsonUtil.TryDeserialize<OpenAiUsageEnvelope>(body, out OpenAiUsageEnvelope? envelope)) return new OpenAiUsage();
            return envelope?.EffectiveUsage ?? new OpenAiUsage();
        }

        private static List<RequestHistoryTimelineEvent> BuildTimeline(RequestContext requestContext, RoutingDecision decision, RequestHistoryTelemetry telemetry, DateTime completedUtc)
        {
            List<RequestHistoryTimelineEvent> events = new List<RequestHistoryTimelineEvent>();
            AddTimelineEvent(events, requestContext, RequestHistoryTimelineStageEnum.Received, "Xeno server received the API request.", requestContext.StartedUtc, true);

            int index = 0;
            foreach (string stage in decision.Timeline)
            {
                DateTime timestamp = requestContext.StartedUtc.AddMilliseconds(Math.Min(Math.Max(1, index + 1), Math.Max(1, (completedUtc - requestContext.StartedUtc).TotalMilliseconds)));
                RequestHistoryTimelineStageEnum timelineStage = ParseTimelineStage(stage);
                AddTimelineEvent(events, requestContext, timelineStage, DescribeTimelineStage(timelineStage, stage), timestamp, true);
                index++;
            }

            if (telemetry.RequestToHeadersMs.HasValue)
            {
                AddTimelineEvent(events, requestContext, RequestHistoryTimelineStageEnum.ResponseHeaders, "Selected client received upstream response headers.", requestContext.StartedUtc.AddMilliseconds(telemetry.RequestToHeadersMs.Value), true);
            }

            if (telemetry.TimeToFirstTokenMs.HasValue)
            {
                AddTimelineEvent(events, requestContext, RequestHistoryTimelineStageEnum.FirstToken, "First response byte or token was received from the upstream provider.", requestContext.StartedUtc.AddMilliseconds(telemetry.TimeToFirstTokenMs.Value), true);
            }

            AddTimelineEvent(events, requestContext, decision.Success ? RequestHistoryTimelineStageEnum.Completed : RequestHistoryTimelineStageEnum.Failed, decision.Message, completedUtc, decision.Success);
            return events;
        }

        private static RequestHistoryTimelineStageEnum ParseTimelineStage(string stage)
        {
            if (String.IsNullOrWhiteSpace(stage)) return RequestHistoryTimelineStageEnum.Unknown;
            return Enum.TryParse(stage, true, out RequestHistoryTimelineStageEnum parsed) ? parsed : RequestHistoryTimelineStageEnum.Unknown;
        }

        private static string DescribeTimelineStage(RequestHistoryTimelineStageEnum stage, string fallback)
        {
            switch (stage)
            {
                case RequestHistoryTimelineStageEnum.ModelResolution:
                    return "Xeno resolved the requested model.";
                case RequestHistoryTimelineStageEnum.ClientInventory:
                    return "Xeno evaluated connected client inventory.";
                case RequestHistoryTimelineStageEnum.ClientSelection:
                    return "Xeno selected the connected client for this request.";
                case RequestHistoryTimelineStageEnum.ClientMissing:
                    return "Selected client record was missing.";
                case RequestHistoryTimelineStageEnum.ClientNotConnected:
                    return "Selected client was not connected.";
                case RequestHistoryTimelineStageEnum.ClientDispatch:
                    return "Xeno dispatched the request to the selected client.";
                case RequestHistoryTimelineStageEnum.ClientResponseStart:
                    return "Selected client started a streamed response.";
                case RequestHistoryTimelineStageEnum.ClientResponse:
                    return "Selected client returned a response.";
                case RequestHistoryTimelineStageEnum.ClientResponseComplete:
                    return "Selected client completed the streamed response.";
                case RequestHistoryTimelineStageEnum.ClientTimeout:
                    return "Selected client timed out.";
                case RequestHistoryTimelineStageEnum.Streaming:
                    return "Selected client streamed response content.";
                default:
                    return String.IsNullOrWhiteSpace(fallback) ? "Timeline stage was recorded." : fallback;
            }
        }

        private static void AddTimelineEvent(List<RequestHistoryTimelineEvent> events, RequestContext requestContext, RequestHistoryTimelineStageEnum stage, string message, DateTime timestampUtc, bool success)
        {
            events.Add(new RequestHistoryTimelineEvent
            {
                Stage = stage,
                Message = message,
                TimestampUtc = timestampUtc,
                ElapsedMs = Math.Max(0, (long)(timestampUtc - requestContext.StartedUtc).TotalMilliseconds),
                Success = success
            });
        }

        private static string? TruncateBody(string? body, out bool truncated)
        {
            truncated = false;
            if (body == null) return null;
            if (body.Length <= MaxCapturedBodyChars) return body;
            truncated = true;
            return body.Substring(0, MaxCapturedBodyChars);
        }

        private static string? RedactSensitiveJsonBody(string? body)
        {
            if (String.IsNullOrWhiteSpace(body)) return body;
            try
            {
                return JsonRedactionHelper.RedactSensitiveJson(body, IsSensitiveBodyField, "(redacted)", false);
            }
            catch (JsonException)
            {
                return body;
            }
        }

        private static bool IsSensitiveBodyField(string? name)
        {
            if (String.IsNullOrWhiteSpace(name)) return false;
            string normalized = name.Replace("_", String.Empty, StringComparison.Ordinal).Replace("-", String.Empty, StringComparison.Ordinal);
            return normalized.Contains("authorization", StringComparison.OrdinalIgnoreCase) ||
                   normalized.Contains("apikey", StringComparison.OrdinalIgnoreCase) ||
                   normalized.Contains("accesskey", StringComparison.OrdinalIgnoreCase) ||
                   normalized.Contains("secret", StringComparison.OrdinalIgnoreCase) ||
                   normalized.Contains("token", StringComparison.OrdinalIgnoreCase) ||
                   normalized.Contains("cookie", StringComparison.OrdinalIgnoreCase) ||
                   normalized.Contains("credential", StringComparison.OrdinalIgnoreCase) ||
                   normalized.Contains("password", StringComparison.OrdinalIgnoreCase);
        }

        private static Dictionary<string, string> CaptureRequestHeaders(HttpContextBase context, Dictionary<string, string> forwardedHeaders)
        {
            Dictionary<string, string> headers = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            foreach (string? key in context.Request.Headers.AllKeys)
            {
                if (String.IsNullOrWhiteSpace(key)) continue;
                headers[key] = context.Request.Headers.Get(key) ?? String.Empty;
            }

            foreach (KeyValuePair<string, string> forwarded in forwardedHeaders)
            {
                headers[forwarded.Key] = forwarded.Value;
            }

            return headers;
        }

        private static Dictionary<string, string> ExtractHeaders(ClientResponseMessage message)
        {
            Dictionary<string, string> headers = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            if (message.Headers == null) return headers;
            foreach (KeyValuePair<string, string> header in message.Headers)
            {
                if (String.IsNullOrWhiteSpace(header.Key) || String.IsNullOrWhiteSpace(header.Value)) continue;
                headers[header.Key] = SanitizeHeaderValue(header.Value);
            }

            return headers;
        }

        private static Dictionary<string, string> RedactHeaders(Dictionary<string, string> headers)
        {
            Dictionary<string, string> redacted = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            foreach (KeyValuePair<string, string> header in headers)
            {
                if (IsSensitiveHeader(header.Key))
                {
                    redacted[header.Key] = "(redacted)";
                }
                else
                {
                    redacted[header.Key] = SanitizeHeaderValue(header.Value);
                }
            }

            return redacted;
        }

        private static bool IsSensitiveHeader(string name)
        {
            return name.Equals("Authorization", StringComparison.OrdinalIgnoreCase) ||
                   name.Equals("Cookie", StringComparison.OrdinalIgnoreCase) ||
                   name.Equals("Set-Cookie", StringComparison.OrdinalIgnoreCase) ||
                   name.Equals("x-secret-key", StringComparison.OrdinalIgnoreCase) ||
                   name.Equals("x-token", StringComparison.OrdinalIgnoreCase) ||
                   name.EndsWith("-key", StringComparison.OrdinalIgnoreCase);
        }

        private static void ApplyClientResponseHeaders(HttpContextBase context, Dictionary<string, string> headers)
        {
            foreach (KeyValuePair<string, string> header in headers)
            {
                if (IsHopByHopHeader(header.Key)) continue;
                if (header.Key.Equals("Content-Type", StringComparison.OrdinalIgnoreCase))
                {
                    context.Response.ContentType = header.Value;
                    continue;
                }

                if (header.Key.Equals("Content-Length", StringComparison.OrdinalIgnoreCase)) continue;
                context.Response.Headers.Add(header.Key, header.Value);
            }
        }

        private static bool IsHopByHopHeader(string name)
        {
            return name.Equals("Connection", StringComparison.OrdinalIgnoreCase) ||
                   name.Equals("Keep-Alive", StringComparison.OrdinalIgnoreCase) ||
                   name.Equals("Proxy-Authenticate", StringComparison.OrdinalIgnoreCase) ||
                   name.Equals("Proxy-Authorization", StringComparison.OrdinalIgnoreCase) ||
                   name.Equals("TE", StringComparison.OrdinalIgnoreCase) ||
                   name.Equals("Trailer", StringComparison.OrdinalIgnoreCase) ||
                   name.Equals("Transfer-Encoding", StringComparison.OrdinalIgnoreCase) ||
                   name.Equals("Upgrade", StringComparison.OrdinalIgnoreCase);
        }

        private static void ApplyRoutingTraceHeaders(
            HttpContextBase context,
            RequestContext requestContext,
            RoutingDecision decision,
            XenoClientRecord? client,
            XenoClientModel? selectedModel,
            RequestHistoryEntry? history)
        {
            string? selectedClientId = FirstNonEmpty(decision.SelectedClientId, client?.Id);
            AddTraceHeader(context, "X-Client-ID", selectedClientId);
            AddTraceHeader(context, "X-Xeno-Request-ID", requestContext.RequestId);
            AddTraceHeader(context, "X-Xeno-Tenant-ID", requestContext.TenantId);
            AddTraceHeader(context, "X-Xeno-Model", decision.EffectiveModel);
            AddTraceHeader(context, "X-Xeno-Routing-Outcome", decision.OutcomeCode);
            AddTraceHeader(context, "X-Xeno-Routing-Message", decision.Message);
            AddTraceHeader(context, "X-Xeno-Routing-Policy-ID", decision.RoutingPolicyId);
            AddTraceHeader(context, "X-Xeno-Routing-Policy-Name", decision.RoutingPolicyName);
            AddTraceHeader(context, "X-Xeno-Routing-Policy-Match", FirstNonEmpty(decision.RoutingPolicyModelName, "*") + "/" + FirstNonEmpty(decision.RoutingPolicyClientName, "*"));
            AddTraceHeader(context, "X-Xeno-Routing-Policy-Reason", decision.RoutingPolicyReason);
            AddTraceHeader(context, "X-Xeno-Routing-Candidate-Count", decision.CandidateCount.ToString(CultureInfo.InvariantCulture));
            AddTraceHeader(context, "X-Xeno-Routing-Candidates", String.Join(",", decision.CandidateClientIds));
            AddTraceHeader(context, "X-Xeno-Load-Balancing-Mode", decision.LoadBalancingMode.ToString());
            AddTraceHeader(context, "X-Xeno-Adaptive-Sample-Count", decision.AdaptiveSampleCount?.ToString(CultureInfo.InvariantCulture));
            AddTraceHeader(context, "X-Xeno-Adaptive-Score", decision.AdaptiveScore);
            AddTraceHeader(context, "X-Xeno-Selection-Reason", decision.SelectionReason);
            AddTraceHeader(context, "X-Xeno-Selected-Client-Name", FirstNonEmpty(decision.SelectedClientName, client?.Name));
            AddTraceHeader(context, "X-Xeno-Selected-API-Type", FirstNonEmpty(decision.SelectedProvider, selectedModel?.Provider.ToString()));
            AddTraceHeader(context, "X-Xeno-Selected-Provider", FirstNonEmpty(decision.SelectedProvider, selectedModel?.Provider.ToString()));
            AddTraceHeader(context, "X-Xeno-Selected-Source", FirstNonEmpty(decision.SelectedSource, selectedModel?.Source));
            AddTraceHeader(context, "X-Xeno-Selected-In-Flight", decision.SelectedClientInFlightRequests?.ToString(CultureInfo.InvariantCulture));
            AddTraceHeader(context, "X-Xeno-Selected-Max-Parallel", decision.SelectedClientMaxParallelRequests?.ToString(CultureInfo.InvariantCulture));
            AddTraceHeader(context, "X-Xeno-Selected-Weight", decision.SelectedClientWeight?.ToString(CultureInfo.InvariantCulture));
            AddTraceHeader(context, "X-Xeno-Selected-Last-Heartbeat-UTC", decision.SelectedClientLastHeartbeatUtc?.ToString("O", CultureInfo.InvariantCulture));
            AddTraceHeader(context, "X-Xeno-Client-Roundtrip-Ms", decision.ClientRoundTripMs?.ToString(CultureInfo.InvariantCulture));
            AddTraceHeader(context, "X-Xeno-Request-Duration-Ms", Math.Max(0, (long)(DateTime.UtcNow - requestContext.StartedUtc).TotalMilliseconds).ToString(CultureInfo.InvariantCulture));
            AddTraceHeader(context, "X-Xeno-Request-History-ID", history?.Id);
            AddTraceHeader(context, "X-Xeno-Routing-Timeline", String.Join(",", decision.Timeline));
            AddTraceHeader(context, "X-Xeno-Time-To-First-Token-Ms", history?.Telemetry.TimeToFirstTokenMs?.ToString(CultureInfo.InvariantCulture));
            AddTraceHeader(context, "X-Xeno-Total-Tokens", history?.Telemetry.TotalTokens?.ToString(CultureInfo.InvariantCulture));
            AddTraceHeader(context, "X-Xeno-Stream-Chunks", history?.Telemetry.StreamChunkCount?.ToString(CultureInfo.InvariantCulture));
        }

        private static Dictionary<string, string> BuildForwardedHeaders(HttpContextBase context, RequestContext requestContext, string requestorIp)
        {
            Dictionary<string, string> headers = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            string sourceIp = requestContext.SourceIp ?? String.Empty;
            string existingForwardedFor = FirstNonEmpty(context.Request.Headers.Get("X-Forwarded-For"), context.Request.Headers.Get("x-forwarded-for"));
            string forwardedFor = existingForwardedFor;
            if (!String.IsNullOrWhiteSpace(sourceIp))
            {
                forwardedFor = String.IsNullOrWhiteSpace(existingForwardedFor)
                    ? sourceIp
                    : existingForwardedFor.Trim() + ", " + sourceIp;
            }

            AddForwardedHeader(headers, "X-Forwarded-For", forwardedFor);
            AddForwardedHeader(headers, "X-Real-IP", requestorIp);
            AddForwardedHeader(headers, "X-Xeno-Requestor-IP", requestorIp);
            AddForwardedHeader(headers, "X-Xeno-Request-ID", requestContext.RequestId);
            AddForwardedHeader(headers, "X-Xeno-Tenant-ID", requestContext.TenantId);
            return headers;
        }

        private static string GetRequestorIp(HttpContextBase context, RequestContext requestContext)
        {
            return SanitizeHeaderValue(requestContext.SourceIp);
        }

        private static void AddForwardedHeader(Dictionary<string, string> headers, string name, string? value)
        {
            string sanitized = SanitizeHeaderValue(value);
            if (String.IsNullOrWhiteSpace(sanitized)) return;
            headers[name] = sanitized;
        }

        private static void AddTraceHeader(HttpContextBase context, string name, string? value)
        {
            string sanitized = SanitizeHeaderValue(value);
            if (String.IsNullOrWhiteSpace(sanitized)) return;
            context.Response.Headers.Add(name, sanitized);
        }

        private static string SanitizeHeaderValue(string? value)
        {
            if (String.IsNullOrWhiteSpace(value)) return String.Empty;
            string sanitized = value.Replace("\r", " ", StringComparison.Ordinal).Replace("\n", " ", StringComparison.Ordinal).Trim();
            return sanitized.Length <= 1024 ? sanitized : sanitized.Substring(0, 1024);
        }

        private async Task SavePlatformAsync(CancellationToken token)
        {
            if (_Database == null) return;
            await _Database.SaveAsync(token).ConfigureAwait(false);
        }

        private async Task SaveSettingsAsync(CancellationToken token)
        {
            string fullPath = System.IO.Path.GetFullPath(_SettingsFile);
            string? directory = System.IO.Path.GetDirectoryName(fullPath);
            if (!String.IsNullOrWhiteSpace(directory) && !Directory.Exists(directory))
            {
                Directory.CreateDirectory(directory);
            }

            await File.WriteAllTextAsync(fullPath, JsonUtil.Serialize(_Settings, true), token).ConfigureAwait(false);
        }

        private void ApplySettings(XenoSettings incoming)
        {
            _Settings.Rest = incoming.Rest ?? new RestSettings();
            _Settings.Cors = incoming.Cors ?? new CorsSettings();
            _Settings.WebSocket = incoming.WebSocket ?? new WebSocketSettings();
            _Settings.Database = incoming.Database ?? new DatabaseSettings();
            _Settings.Logging = incoming.Logging ?? new LoggingSettings();
            _Settings.Auth = incoming.Auth ?? new AuthSettings();
            _Settings.RequestHistory = incoming.RequestHistory ?? new RequestHistorySettings();
            _Settings.Routing = incoming.Routing ?? new RoutingSettings();
            _Settings.Email = incoming.Email ?? new EmailSettings();
            _Settings.Ledger = incoming.Ledger ?? new LedgerSettings();
            ApplyEnvironmentSettings(_Settings);
            if (_Database != null)
            {
                _Routing = new RoutingService(_Database.Platform, _Settings.Routing);
                _Ledger?.Dispose();
                _Ledger = new LedgerService(_Database.Platform, _Settings.Ledger, SavePlatformAsync);
                _Ledger.Start();
                _NetLedgerTenantSubordinate?.Dispose();
                _NetLedgerTenantSubordinate = new NetLedgerTenantSubordinateService(_Database.Platform, _Settings.Ledger);
                _TenantLifecycle = new TenantLifecycleService(_Database.Platform, new ITenantSubordinateService[] { _NetLedgerTenantSubordinate });
            }
        }

        private static string NormalizeOpenAiPath(string path)
        {
            const string ApiPrefix = "/api/v1/openai";
            if (path.StartsWith(ApiPrefix, StringComparison.OrdinalIgnoreCase))
            {
                return path.Substring(ApiPrefix.Length);
            }

            return path;
        }

        private async Task HandleCreateTokenAsync(HttpContextBase context, RequestContext requestContext, CancellationToken token)
        {
            AuthTokenRequest body = ReadJsonBody<AuthTokenRequest>(context);
            string accessKey = FirstNonEmpty(body.AccessKey, context.Request.Headers.Get("x-access-key"));
            string secretKey = FirstNonEmpty(body.SecretKey, context.Request.Headers.Get("x-secret-key"));
            if (!String.IsNullOrWhiteSpace(secretKey)) throw new UnauthorizedAccessException("Secret keys are not accepted for credential token authentication.");

            XenoPlatform platform = RequirePlatform();
            Credential? credential = _Authentication.ValidateCredential(platform, requestContext.TenantId, accessKey);
            if (credential == null)
            {
                _Authentication.AppendAudit(platform, requestContext, "Authentication", OperationTypeEnum.Read, false, "InvalidCredential", 401);
                await SavePlatformAsync(token).ConfigureAwait(false);
                throw new UnauthorizedAccessException("Invalid credential.");
            }

            AuthTokenResult credentialResult = _Authentication.IssueToken(
                platform,
                _Settings.Auth,
                requestContext.TenantId,
                PrincipalTypeEnum.Credential,
                credential.UserId,
                credential.Id,
                AuthSchemeEnum.Credential);
            await SavePlatformAsync(token).ConfigureAwait(false);
            await SendJsonAsync(context, credentialResult).ConfigureAwait(false);
        }

        private async Task HandleLoginAsync(HttpContextBase context, CancellationToken token)
        {
            AuthLoginRequest body = ReadJsonBody<AuthLoginRequest>(context);
            string tenantId = ResolveTenantId(context, body.TenantId);
            string username = FirstNonEmpty(body.Username, body.Email);
            string password = FirstNonEmpty(body.Password);
            if (String.IsNullOrWhiteSpace(username) || String.IsNullOrWhiteSpace(password))
            {
                throw new ArgumentException("Username and password are required.");
            }

            XenoPlatform platform = RequirePlatform();
            User? user = _Authentication.ValidateUserPassword(platform, tenantId, username, password);
            RequestContext loginContext = new RequestContext
            {
                TenantId = tenantId,
                PrincipalType = PrincipalTypeEnum.User,
                UserId = user?.Id,
                IsAuthenticated = user != null,
                AuthScheme = AuthSchemeEnum.Password
            };
            ApplyRequestMetadata(context, loginContext);

            if (user == null)
            {
                _Authentication.AppendAudit(platform, loginContext, "Authentication", OperationTypeEnum.Read, false, "InvalidUserPassword", 401);
                await SavePlatformAsync(token).ConfigureAwait(false);
                throw new UnauthorizedAccessException("Invalid username or password.");
            }

            Credential credential = new Credential
            {
                TenantId = tenantId,
                UserId = user.Id,
                AccessKey = IdGenerator.NewId("xeno_"),
                SecretKey = _Authentication.HashCredentialSecret(_Authentication.GenerateSecret()),
                Active = true
            };
            platform.Credentials.Add(credential);
            _Authentication.AppendAudit(platform, loginContext, "Authentication", OperationTypeEnum.Write, true, "ClientCredentialIssued", 200);
            await SavePlatformAsync(token).ConfigureAwait(false);
            await SendJsonAsync(context, new
            {
                id = credential.Id,
                tenantId = credential.TenantId,
                userId = credential.UserId,
                accessKey = credential.AccessKey,
                authScheme = "Credential"
            }).ConfigureAwait(false);
        }

        private async Task HandleLoginTenantLookupAsync(HttpContextBase context)
        {
            AuthLoginRequest body = ReadJsonBody<AuthLoginRequest>(context);
            string email = FirstNonEmpty(body.Email, body.Username, DecodeQueryValue(context.Request.Query.Elements.Get("email"))).Trim();
            if (String.IsNullOrWhiteSpace(email))
            {
                throw new ArgumentException("Email is required.");
            }

            XenoPlatform platform = RequirePlatform();
            List<AuthTenantSummary> tenants = platform.Users
                .Where(user => user.Active && String.Equals(user.Email, email, StringComparison.OrdinalIgnoreCase))
                .Join(
                    platform.Tenants.Where(tenant => tenant.Active),
                    user => user.TenantId,
                    tenant => tenant.Id,
                    (user, tenant) => new AuthTenantSummary
                    {
                        Id = tenant.Id,
                        Name = tenant.Name
                    })
                .GroupBy(tenant => tenant.Id, StringComparer.OrdinalIgnoreCase)
                .Select(group => group.First())
                .OrderBy(tenant => tenant.Name)
                .ToList();

            AuthTenantLookupResponse response = new AuthTenantLookupResponse
            {
                Email = email,
                Tenants = tenants
            };
            await SendJsonAsync(context, response).ConfigureAwait(false);
        }

        private async Task HandleDashboardLoginAsync(HttpContextBase context, CancellationToken token)
        {
            AuthLoginRequest body = ReadJsonBody<AuthLoginRequest>(context);
            string tenantId = ResolveTenantId(context, body.TenantId);
            string email = FirstNonEmpty(body.Email, body.Username).Trim();
            string password = FirstNonEmpty(body.Password);
            if (String.IsNullOrWhiteSpace(email) || String.IsNullOrWhiteSpace(password))
            {
                throw new ArgumentException("Email and password are required.");
            }

            XenoPlatform platform = RequirePlatform();
            User? user = _Authentication.ValidateUserPassword(platform, tenantId, email, password);
            RequestContext loginContext = new RequestContext
            {
                TenantId = tenantId,
                PrincipalType = PrincipalTypeEnum.User,
                UserId = user?.Id,
                IsAuthenticated = user != null,
                AuthScheme = AuthSchemeEnum.Password
            };
            ApplyRequestMetadata(context, loginContext);

            if (user == null)
            {
                _Authentication.AppendAudit(platform, loginContext, "Authentication", OperationTypeEnum.Read, false, "InvalidDashboardLogin", 401);
                await SavePlatformAsync(token).ConfigureAwait(false);
                throw new UnauthorizedAccessException("Invalid email, tenant, or password.");
            }

            AuthTokenResult tokenResult = _Authentication.IssueToken(
                platform,
                _Settings.Auth,
                tenantId,
                PrincipalTypeEnum.User,
                user.Id,
                null,
                AuthSchemeEnum.Bearer);
            Tenant? tenant = platform.Tenants.FirstOrDefault(item => item.Id == tenantId);
            _Authentication.AppendAudit(platform, loginContext, "Authentication", OperationTypeEnum.Read, true, "DashboardLogin", 200);
            await SavePlatformAsync(token).ConfigureAwait(false);

            AuthDashboardLoginResponse response = new AuthDashboardLoginResponse
            {
                Token = tokenResult.Token,
                TokenType = tokenResult.TokenType,
                TenantId = tokenResult.TenantId,
                TenantName = tenant?.Name ?? tokenResult.TenantId,
                UserId = user.Id,
                Email = user.Email,
                SessionId = tokenResult.SessionId,
                ExpiresUtc = tokenResult.ExpiresUtc
            };
            await SendJsonAsync(context, response).ConfigureAwait(false);
        }

        private async Task HandleOnboardingSignupAsync(HttpContextBase context, CancellationToken token)
        {
            AuthOnboardingRequest body = ReadJsonBody<AuthOnboardingRequest>(context);
            string requestedTenantId = FirstNonEmpty(body.TenantId).Trim();
            string tenantName = FirstNonEmpty(body.TenantName).Trim();
            string email = FirstNonEmpty(body.Email).Trim();
            string username = FirstNonEmpty(body.Username, email).Trim();
            string password = FirstNonEmpty(body.Password);
            if (String.IsNullOrWhiteSpace(email)) throw new ArgumentException("Email is required.");
            if (String.IsNullOrWhiteSpace(username)) throw new ArgumentException("Username is required.");
            if (String.IsNullOrWhiteSpace(password)) throw new ArgumentException("Password is required.");
            if (!IsValidEmailAddress(email)) throw new ArgumentException("Email address is invalid.");
            if (password.Length < 8) throw new ArgumentException("Password must be at least 8 characters.");
            if (username.Length > 256) throw new ArgumentException("Username must be 256 characters or fewer.");
            if (!String.IsNullOrWhiteSpace(requestedTenantId) && requestedTenantId.Length > 64) throw new ArgumentException("Tenant ID must be 64 characters or fewer.");
            if (!String.IsNullOrWhiteSpace(tenantName) && tenantName.Length > 256) throw new ArgumentException("Tenant name must be 256 characters or fewer.");

            XenoPlatform platform = RequirePlatform();
            if (platform.Users.Any(item => item.IsAdmin && String.Equals(item.Email, email, StringComparison.OrdinalIgnoreCase)))
            {
                throw new HttpStatusException(409, "GlobalAdminEmailAlreadyExists", "Email already belongs to a global administrator.");
            }

            bool creatingTenant = String.IsNullOrWhiteSpace(requestedTenantId);
            Tenant tenant;
            if (creatingTenant)
            {
                if (String.IsNullOrWhiteSpace(tenantName)) throw new ArgumentException("Tenant name is required when tenantId is omitted.");
                tenant = new Tenant
                {
                    Id = IdGenerator.NewId("ten_"),
                    Name = tenantName,
                    Active = true,
                    CreatedUtc = DateTime.UtcNow,
                    LastUpdateUtc = DateTime.UtcNow
                };
                platform.Tenants.Add(tenant);
                platform.EnsureTenantDefaults(tenant.Id);
                RemoveSeedDevelopmentPrincipal(platform, tenant.Id);
            }
            else
            {
                Tenant? existingTenant = platform.Tenants.FirstOrDefault(item => String.Equals(item.Id, requestedTenantId, StringComparison.OrdinalIgnoreCase));
                if (existingTenant == null) throw new HttpStatusException(404, "TenantNotFound", "Tenant ID was not found.");
                if (!existingTenant.Active) throw new HttpStatusException(409, "TenantInactive", "Tenant is inactive.");
                tenant = existingTenant;
            }
            platform.EnsureTenantDefaults(tenant.Id);

            if (platform.Users.Any(item => item.TenantId == tenant.Id && String.Equals(item.Email, email, StringComparison.OrdinalIgnoreCase))) throw new HttpStatusException(409, "EmailAlreadyExists", "Email already exists in the tenant.");
            if (platform.Users.Any(item => item.TenantId == tenant.Id && String.Equals(item.Username, username, StringComparison.OrdinalIgnoreCase))) throw new HttpStatusException(409, "UsernameAlreadyExists", "Username already exists in the tenant.");

            User user = new User
            {
                TenantId = tenant.Id,
                Username = username,
                Email = email,
                IsAdmin = false,
                IsTenantAdmin = creatingTenant,
                Active = true
            };
            _Authentication.SetUserPassword(user, password);
            platform.Users.Add(user);

            string accessKey = IdGenerator.NewId("xeno_");
            while (platform.Credentials.Any(item => item.TenantId == tenant.Id && String.Equals(item.AccessKey, accessKey, StringComparison.Ordinal)))
            {
                accessKey = IdGenerator.NewId("xeno_");
            }

            string secretKey = _Authentication.GenerateSecret();
            Credential credential = new Credential
            {
                TenantId = tenant.Id,
                UserId = user.Id,
                AccessKey = accessKey,
                SecretKey = _Authentication.HashCredentialSecret(secretKey),
                Active = true
            };
            platform.Credentials.Add(credential);

            Role? tenantAdminRole = platform.Roles.FirstOrDefault(item => item.TenantId == tenant.Id && String.Equals(item.Name, "TenantAdmin", StringComparison.OrdinalIgnoreCase) && item.Active);
            if (creatingTenant && tenantAdminRole != null && !platform.RoleAssignments.Any(item => item.TenantId == tenant.Id && item.RoleId == tenantAdminRole.Id && item.PrincipalType == PrincipalTypeEnum.User && item.PrincipalId == user.Id))
            {
                platform.RoleAssignments.Add(new RoleAssignment
                {
                    TenantId = tenant.Id,
                    RoleId = tenantAdminRole.Id,
                    PrincipalType = PrincipalTypeEnum.User,
                    PrincipalId = user.Id,
                    Active = true,
                    CreatedUtc = DateTime.UtcNow,
                    LastUpdateUtc = DateTime.UtcNow
                });
            }

            if (creatingTenant && tenantAdminRole != null && !platform.RoleAssignments.Any(item => item.TenantId == tenant.Id && item.RoleId == tenantAdminRole.Id && item.PrincipalType == PrincipalTypeEnum.Credential && item.PrincipalId == credential.Id))
            {
                platform.RoleAssignments.Add(new RoleAssignment
                {
                    TenantId = tenant.Id,
                    RoleId = tenantAdminRole.Id,
                    PrincipalType = PrincipalTypeEnum.Credential,
                    PrincipalId = credential.Id,
                    Active = true,
                    CreatedUtc = DateTime.UtcNow,
                    LastUpdateUtc = DateTime.UtcNow
                });
            }

            if (!creatingTenant)
            {
                Role? tenantMemberRole = platform.Roles.FirstOrDefault(item => item.TenantId == tenant.Id && String.Equals(item.Name, "TenantMember", StringComparison.OrdinalIgnoreCase) && item.Active);
                if (tenantMemberRole != null && !platform.RoleAssignments.Any(item => item.TenantId == tenant.Id && item.RoleId == tenantMemberRole.Id && item.PrincipalType == PrincipalTypeEnum.User && item.PrincipalId == user.Id))
                {
                    platform.RoleAssignments.Add(new RoleAssignment
                    {
                        TenantId = tenant.Id,
                        RoleId = tenantMemberRole.Id,
                        PrincipalType = PrincipalTypeEnum.User,
                        PrincipalId = user.Id,
                        Active = true,
                        CreatedUtc = DateTime.UtcNow,
                        LastUpdateUtc = DateTime.UtcNow
                    });
                }

                if (tenantMemberRole != null && !platform.RoleAssignments.Any(item => item.TenantId == tenant.Id && item.RoleId == tenantMemberRole.Id && item.PrincipalType == PrincipalTypeEnum.Credential && item.PrincipalId == credential.Id))
                {
                    platform.RoleAssignments.Add(new RoleAssignment
                    {
                        TenantId = tenant.Id,
                        RoleId = tenantMemberRole.Id,
                        PrincipalType = PrincipalTypeEnum.Credential,
                        PrincipalId = credential.Id,
                        Active = true,
                        CreatedUtc = DateTime.UtcNow,
                        LastUpdateUtc = DateTime.UtcNow
                    });
                }
            }

            RequestContext signupContext = new RequestContext
            {
                TenantId = tenant.Id,
                PrincipalType = PrincipalTypeEnum.User,
                UserId = user.Id,
                IsAuthenticated = true,
                AuthScheme = AuthSchemeEnum.Password
            };
            ApplyRequestMetadata(context, signupContext);
            signupContext.CorrelationId = FirstNonEmpty(signupContext.CorrelationId, signupContext.RequestId);
            DateTime eventUtc = DateTime.UtcNow;
            if (creatingTenant)
            {
                await RequireTenantLifecycle().CreateAsync(tenant, new List<User> { user }, body.DeferSubordinateProvisioning == true, signupContext, token).ConfigureAwait(false);
            }
            else
            {
                await ProvisionLedgerAccountForUserAsync(user, signupContext, token).ConfigureAwait(false);
            }

            AuthTokenResult tokenResult = _Authentication.IssueToken(platform, _Settings.Auth, tenant.Id, PrincipalTypeEnum.User, user.Id, null, AuthSchemeEnum.Bearer);
            _Authentication.AppendAudit(platform, signupContext, "Onboarding", OperationTypeEnum.Write, true, "OnboardingSignup", 200);
            await SavePlatformAsync(token).ConfigureAwait(false);

            AuthOnboardingResponse response = new AuthOnboardingResponse
            {
                Status = "success",
                Tenant = new AuthOnboardingTenantDetails
                {
                    Id = tenant.Id,
                    Name = tenant.Name,
                    Active = tenant.Active,
                    CreatedUtc = tenant.CreatedUtc,
                    LastUpdateUtc = tenant.LastUpdateUtc
                },
                User = new AuthOnboardingUserDetails
                {
                    Id = user.Id,
                    TenantId = user.TenantId,
                    Username = user.Username,
                    Email = user.Email,
                    IsAdmin = user.IsAdmin,
                    IsTenantAdmin = user.IsTenantAdmin,
                    Active = user.Active
                },
                Credential = new AuthOnboardingCredentialDetails
                {
                    Id = credential.Id,
                    TenantId = credential.TenantId,
                    UserId = credential.UserId,
                    AccessKey = credential.AccessKey,
                    SecretKey = secretKey,
                    Active = credential.Active
                },
                Token = new AuthOnboardingTokenDetails
                {
                    Token = tokenResult.Token,
                    TokenType = tokenResult.TokenType,
                    TenantId = tokenResult.TenantId,
                    UserId = user.Id,
                    SessionId = tokenResult.SessionId,
                    ExpiresUtc = tokenResult.ExpiresUtc
                },
                Request = BuildOnboardingRequestMetadata(signupContext, eventUtc)
            };

            OnboardingEmailContext emailContext = BuildOnboardingEmailContext(platform, signupContext, tenant, user, creatingTenant, eventUtc);
            await SendOnboardingNotificationsSafeAsync(emailContext, signupContext, token).ConfigureAwait(false);
            await SendJsonAsync(context, response).ConfigureAwait(false);
        }

        private static void RemoveSeedDevelopmentPrincipal(XenoPlatform platform, string tenantId)
        {
            List<string> seedUserIds = platform.Users
                .Where(item => item.TenantId == tenantId && String.Equals(item.Email, "admin@xeno", StringComparison.OrdinalIgnoreCase))
                .Select(item => item.Id)
                .ToList();
            if (seedUserIds.Count == 0) return;

            platform.Credentials.RemoveAll(item => item.TenantId == tenantId && (seedUserIds.Contains(item.UserId) || String.Equals(item.AccessKey, "default", StringComparison.Ordinal)));
            platform.RoleAssignments.RemoveAll(item => item.TenantId == tenantId && item.PrincipalType == PrincipalTypeEnum.User && seedUserIds.Contains(item.PrincipalId));
            platform.Users.RemoveAll(item => item.TenantId == tenantId && seedUserIds.Contains(item.Id));
        }

        private OnboardingEmailContext BuildOnboardingEmailContext(XenoPlatform platform, RequestContext requestContext, Tenant tenant, User user, bool createdTenant, DateTime eventUtc)
        {
            return new OnboardingEmailContext
            {
                CreatedTenant = createdTenant,
                TenantId = tenant.Id,
                TenantName = tenant.Name,
                UserId = user.Id,
                Username = user.Username,
                UserEmail = user.Email,
                IsTenantAdmin = user.IsTenantAdmin,
                SourceIp = requestContext.SourceIp ?? String.Empty,
                ForwardedFor = requestContext.ForwardedFor ?? String.Empty,
                UserAgent = requestContext.UserAgent ?? String.Empty,
                Origin = requestContext.Origin ?? String.Empty,
                Referer = requestContext.Referer ?? String.Empty,
                Host = requestContext.Host ?? String.Empty,
                RequestMethod = requestContext.RequestMethod ?? String.Empty,
                RequestPath = requestContext.RequestPath ?? String.Empty,
                RequestId = requestContext.RequestId,
                CorrelationId = requestContext.CorrelationId ?? String.Empty,
                EventUtc = eventUtc,
                PlatformAdministrators = BuildConfiguredEmailRecipients(_Settings.Email.Notifications.PlatformAdministratorRecipients),
                TenantAdministrators = platform.Users
                    .Where(item => item.TenantId == tenant.Id && item.Active && item.IsTenantAdmin && !String.IsNullOrWhiteSpace(item.Email))
                    .Select(item => EmailAddress.Create(item.Email, item.Username))
                    .ToList()
            };
        }

        private static AuthOnboardingRequestMetadata BuildOnboardingRequestMetadata(RequestContext context, DateTime eventUtc)
        {
            return new AuthOnboardingRequestMetadata
            {
                RequestId = context.RequestId,
                CorrelationId = context.CorrelationId ?? String.Empty,
                Method = context.RequestMethod ?? String.Empty,
                Path = context.RequestPath ?? String.Empty,
                SourceIp = context.SourceIp ?? String.Empty,
                ForwardedFor = context.ForwardedFor ?? String.Empty,
                UserAgent = context.UserAgent ?? String.Empty,
                Origin = context.Origin ?? String.Empty,
                Referer = context.Referer ?? String.Empty,
                Host = context.Host ?? String.Empty,
                EventUtc = eventUtc
            };
        }

        private static List<EmailAddress> BuildConfiguredEmailRecipients(List<EmailRecipientSettings> recipients)
        {
            List<EmailAddress> results = new List<EmailAddress>();
            if (recipients == null) return results;

            foreach (EmailRecipientSettings recipient in recipients)
            {
                if (recipient == null || String.IsNullOrWhiteSpace(recipient.Email)) continue;
                results.Add(EmailAddress.Create(recipient.Email, recipient.Name));
            }

            return results;
        }

        private async Task SendOnboardingNotificationsSafeAsync(OnboardingEmailContext emailContext, RequestContext auditContext, CancellationToken token)
        {
            if (!_Settings.Email.Enabled) return;

            try
            {
                using (CancellationTokenSource timeout = CancellationTokenSource.CreateLinkedTokenSource(token))
                {
                    timeout.CancelAfter(TimeSpan.FromSeconds(5));
                    IReadOnlyList<EmailSendResult> results = await _Email.SendOnboardingNotificationsAsync(emailContext, timeout.Token).ConfigureAwait(false);
                    foreach (EmailSendResult result in results)
                    {
                        RecordEmailResult(result);
                        string reasonCode = result.Skipped ? "EmailSkipped" : "EmailAccepted";
                        _Authentication.AppendAudit(RequirePlatform(), auditContext, "Email", OperationTypeEnum.Write, !result.Skipped, reasonCode, result.Skipped ? 202 : 200, emailContext.TenantId, emailContext.UserId);
                    }
                }

                await SavePlatformAsync(token).ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
                RecordEmailFailure(_Settings.Email.Provider.ToString(), "onboarding");
                _Authentication.AppendAudit(RequirePlatform(), auditContext, "Email", OperationTypeEnum.Write, false, "EmailCanceled", 499, emailContext.TenantId, emailContext.UserId);
                await SavePlatformAsync(CancellationToken.None).ConfigureAwait(false);
            }
            catch (EmailDeliveryException)
            {
                RecordEmailFailure(_Settings.Email.Provider.ToString(), "onboarding");
                _Authentication.AppendAudit(RequirePlatform(), auditContext, "Email", OperationTypeEnum.Write, false, "EmailDeliveryFailed", 502, emailContext.TenantId, emailContext.UserId);
                await SavePlatformAsync(CancellationToken.None).ConfigureAwait(false);
            }
            catch (InvalidOperationException)
            {
                RecordEmailFailure(_Settings.Email.Provider.ToString(), "onboarding");
                _Authentication.AppendAudit(RequirePlatform(), auditContext, "Email", OperationTypeEnum.Write, false, "EmailInvalidOperation", 500, emailContext.TenantId, emailContext.UserId);
                await SavePlatformAsync(CancellationToken.None).ConfigureAwait(false);
            }
            catch (ArgumentException)
            {
                RecordEmailFailure(_Settings.Email.Provider.ToString(), "onboarding");
                _Authentication.AppendAudit(RequirePlatform(), auditContext, "Email", OperationTypeEnum.Write, false, "EmailConfigurationInvalid", 500, emailContext.TenantId, emailContext.UserId);
                await SavePlatformAsync(CancellationToken.None).ConfigureAwait(false);
            }
        }

        private async Task<EmailTestResponse> SendAdministrativeTestEmailAsync(EmailTestRequest request, RequestContext auditContext, CancellationToken token)
        {
            token.ThrowIfCancellationRequested();
            if (request == null) throw new ArgumentNullException(nameof(request));

            string toEmail = FirstNonEmpty(request.ToEmail).Trim();
            string toName = FirstNonEmpty(request.ToName).Trim();
            string title = FirstNonEmpty(request.Title, "Xeno Test Email").Trim();
            string subject = FirstNonEmpty(request.Subject, "Xeno test email").Trim();
            string body = FirstNonEmpty(request.Body).Trim();
            if (String.IsNullOrWhiteSpace(toEmail)) throw new ArgumentException("Recipient email address is required.");
            if (!IsValidEmailAddress(toEmail)) throw new ArgumentException("Recipient email address is invalid.");
            if (String.IsNullOrWhiteSpace(body)) throw new ArgumentException("Email body is required.");

            AdministrativeTestEmailTemplate template = new AdministrativeTestEmailTemplate(_Settings.Email);
            EmailTemplateRenderResult rendered = await _Email.RenderTemplateAsync(
                template,
                new AdministrativeTestEmailTemplateContext
                {
                    Title = title,
                    Subject = subject,
                    Body = body
                },
                token).ConfigureAwait(false);
            EmailMessage message = new EmailMessage
            {
                From = EmailAddress.Create(_Settings.Email.FromAddress, _Settings.Email.FromName),
                ReplyTo = String.IsNullOrWhiteSpace(_Settings.Email.ReplyToAddress) ? null : EmailAddress.Create(_Settings.Email.ReplyToAddress),
                Subject = rendered.Subject,
                HtmlBody = rendered.HtmlBody,
                TextBody = rendered.TextBody,
                TemplateId = rendered.TemplateId,
                CorrelationId = auditContext.CorrelationId
            };
            message.To.Add(EmailAddress.Create(toEmail, toName));
            message.Tags["workflow"] = "administrative-test";

            try
            {
                EmailSendResult result = await _Email.SendAsync(message, token).ConfigureAwait(false);
                RecordEmailResult(result);
                string reasonCode = result.Skipped ? "TestEmailSkipped" : "TestEmailAccepted";
                int statusCode = result.Skipped ? 202 : 200;
                _Authentication.AppendAudit(RequirePlatform(), auditContext, "Email", OperationTypeEnum.Write, !result.Skipped, reasonCode, statusCode, auditContext.TenantId, auditContext.UserId);
                await SavePlatformAsync(token).ConfigureAwait(false);
                return new EmailTestResponse
                {
                    Accepted = result.Accepted,
                    Skipped = result.Skipped,
                    Provider = result.Provider,
                    ProviderMessageId = result.ProviderMessageId,
                    StatusMessage = result.StatusMessage,
                    TemplateId = result.TemplateId,
                    ToEmail = toEmail,
                    Subject = rendered.Subject,
                    SentUtc = result.SentUtc
                };
            }
            catch (EmailDeliveryException)
            {
                RecordEmailFailure(_Settings.Email.Provider.ToString(), template.TemplateId);
                _Authentication.AppendAudit(RequirePlatform(), auditContext, "Email", OperationTypeEnum.Write, false, "TestEmailDeliveryFailed", 502, auditContext.TenantId, auditContext.UserId);
                await SavePlatformAsync(CancellationToken.None).ConfigureAwait(false);
                throw new HttpStatusException(502, "EmailDeliveryFailed", "Test email delivery failed.");
            }
            catch (InvalidOperationException)
            {
                RecordEmailFailure(_Settings.Email.Provider.ToString(), template.TemplateId);
                _Authentication.AppendAudit(RequirePlatform(), auditContext, "Email", OperationTypeEnum.Write, false, "TestEmailInvalidOperation", 500, auditContext.TenantId, auditContext.UserId);
                await SavePlatformAsync(CancellationToken.None).ConfigureAwait(false);
                throw;
            }
        }

        private async Task RequirePermissionAsync(RequestContext context, string resourceType, OperationTypeEnum operationType, CancellationToken token)
        {
            await RequirePermissionAsync(context, context.TenantId, resourceType, operationType, token).ConfigureAwait(false);
        }

        private async Task RequirePermissionAsync(RequestContext context, string targetTenantId, string resourceType, OperationTypeEnum operationType, CancellationToken token)
        {
            AuthorizationDecision decision = _Authorization.Authorize(RequirePlatform(), new AuthorizationRequest
            {
                Context = context,
                TargetTenantId = targetTenantId,
                ResourceType = resourceType,
                OperationType = operationType
            });

            if (!decision.Allowed)
            {
                string reasonCode = String.Equals(resourceType, "Inference", StringComparison.OrdinalIgnoreCase) && decision.StatusCode == 403
                    ? "OpenAiPermissionDenied"
                    : decision.ReasonCode;
                string message = String.Equals(reasonCode, "OpenAiPermissionDenied", StringComparison.Ordinal)
                    ? "OpenAI-compatible API permission denied."
                    : decision.Message;
                _Authentication.AppendAudit(RequirePlatform(), context, resourceType, operationType, false, reasonCode, decision.StatusCode, targetTenantId, null);
                await SavePlatformAsync(token).ConfigureAwait(false);
                throw new AuthorizationDeniedException(decision.StatusCode, reasonCode, message);
            }

            if (String.Equals(decision.MatchedSource, "SystemAdmin", StringComparison.Ordinal) || String.Equals(decision.MatchedSource, "TenantAdmin", StringComparison.Ordinal))
            {
                Interlocked.Increment(ref _PrivilegedBypassCount);
                if (operationType != OperationTypeEnum.Read)
                {
                    _Authentication.AppendAudit(RequirePlatform(), context, resourceType, operationType, true, decision.MatchedSource ?? "PrivilegedBypass", 200, targetTenantId, null);
                    await SavePlatformAsync(token).ConfigureAwait(false);
                }
            }
        }

        private async Task RequireUserUpdatePermissionAsync(RequestContext context, string userId, CancellationToken token)
        {
            AuthorizationDecision decision = _Authorization.Authorize(RequirePlatform(), new AuthorizationRequest
            {
                Context = context,
                TargetTenantId = context.TenantId,
                ResourceType = "Users",
                OperationType = OperationTypeEnum.Write
            });
            if (decision.Allowed) return;
            if (String.Equals(context.UserId, userId, StringComparison.Ordinal))
            {
                User? self = RequirePlatform().Users.FirstOrDefault(item => item.Id == userId && item.TenantId == context.TenantId && item.Active);
                if (self != null) return;
            }

            _Authentication.AppendAudit(RequirePlatform(), context, "Users", OperationTypeEnum.Write, false, decision.ReasonCode, decision.StatusCode, context.TenantId, userId);
            await SavePlatformAsync(token).ConfigureAwait(false);
            throw new AuthorizationDeniedException(decision.StatusCode, decision.ReasonCode, decision.Message);
        }

        private List<User> ListVisibleUsers(RequestContext requestContext)
        {
            XenoPlatform platform = RequirePlatform();
            if (CanManageTenant(requestContext, requestContext.TenantId))
            {
                return platform.Users.Where(item => IsTenantVisible(requestContext, item.TenantId)).ToList();
            }

            return platform.Users.Where(item => item.TenantId == requestContext.TenantId && item.Id == requestContext.UserId).ToList();
        }

        private List<Credential> ListVisibleCredentials(RequestContext requestContext)
        {
            XenoPlatform platform = RequirePlatform();
            if (CanManageTenant(requestContext, requestContext.TenantId))
            {
                return platform.Credentials.Where(item => item.TenantId == requestContext.TenantId).ToList();
            }

            return platform.Credentials.Where(item => item.TenantId == requestContext.TenantId && item.UserId == requestContext.UserId).ToList();
        }

        private bool CanManageTenant(RequestContext requestContext, string tenantId)
        {
            XenoPlatform platform = RequirePlatform();
            return _Authorization.IsSystemAdmin(platform, requestContext) || _Authorization.IsTenantAdmin(platform, requestContext, tenantId);
        }

        private async Task<bool> RejectRateLimitedAuthAttemptAsync(HttpContextBase context)
        {
            int limit = _Settings.Auth.MaxLoginRequestsPerMinute;
            if (limit <= 0) return false;

            string sourceIp = String.IsNullOrWhiteSpace(context.Request.Source.IpAddress)
                ? "unknown"
                : context.Request.Source.IpAddress;
            DateTime now = DateTime.UtcNow;
            DateTime cutoff = now.AddMinutes(-1);
            bool limited = false;
            lock (_AuthRateLimitLock)
            {
                if (!_AuthRateLimitAttempts.TryGetValue(sourceIp, out List<DateTime>? attempts))
                {
                    attempts = new List<DateTime>();
                    _AuthRateLimitAttempts[sourceIp] = attempts;
                }

                attempts.RemoveAll(item => item < cutoff);
                if (attempts.Count >= limit)
                {
                    limited = true;
                }
                else
                {
                    attempts.Add(now);
                }

                foreach (string staleKey in _AuthRateLimitAttempts.Where(item => item.Value.Count == 0 || item.Value.All(timestamp => timestamp < cutoff)).Select(item => item.Key).ToList())
                {
                    _AuthRateLimitAttempts.Remove(staleKey);
                }
            }

            if (!limited) return false;
            Interlocked.Increment(ref _AuthFailureCount);
            context.Response.Headers.Add("Retry-After", "60");
            await SendStatusJsonAsync(context, 429, new { error = "RateLimited", message = "Too many authentication attempts. Try again later." }).ConfigureAwait(false);
            return true;
        }

        private Tenant CreateTenant(TenantSaveRequest body)
        {
            XenoPlatform platform = RequirePlatform();
            string id = FirstNonEmpty(body.Id, IdGenerator.NewId("ten_"));
            string name = FirstNonEmpty(body.Name);
            if (String.IsNullOrWhiteSpace(name)) throw new ArgumentException("Tenant name is required.");
            if (platform.Tenants.Any(item => String.Equals(item.Id, id, StringComparison.OrdinalIgnoreCase))) throw new ArgumentException("Tenant ID already exists.");

            Tenant tenant = new Tenant
            {
                Id = id.Trim(),
                Name = name.Trim(),
                Active = true,
                CreatedUtc = DateTime.UtcNow,
                LastUpdateUtc = DateTime.UtcNow
            };
            if (body.Active.HasValue) tenant.Active = body.Active.Value;
            return tenant;
        }

        private User CreateUser(RequestContext requestContext, HttpContextBase context)
        {
            XenoPlatform platform = RequirePlatform();
            UserSaveRequest body = ReadJsonBody<UserSaveRequest>(context);
            string tenantId = FirstNonEmpty(body.TenantId, requestContext.TenantId);
            if (!IsTenantVisible(requestContext, tenantId)) throw new AuthorizationDeniedException(403, "TenantMismatch", "Authenticated tenant cannot access the requested tenant.");
            if (!platform.Tenants.Any(item => item.Id == tenantId && item.Active)) throw new ArgumentException("Tenant does not exist or is inactive.");

            string username = FirstNonEmpty(body.Username, body.Email);
            if (String.IsNullOrWhiteSpace(username)) throw new ArgumentException("Username is required.");
            if (platform.Users.Any(item => item.TenantId == tenantId && String.Equals(item.Username, username, StringComparison.OrdinalIgnoreCase))) throw new ArgumentException("Username already exists in the tenant.");

            string password = FirstNonEmpty(body.Password);
            if (String.IsNullOrWhiteSpace(password)) throw new ArgumentException("Password is required.");

            User user = new User
            {
                TenantId = tenantId,
                Username = username.Trim(),
                Email = FirstNonEmpty(body.Email, username).Trim(),
                IsAdmin = false,
                IsTenantAdmin = false,
                Active = true
            };
            if (body.IsAdmin.HasValue && body.IsAdmin.Value)
            {
                EnsureCanChangeSystemAdminFlag(requestContext);
                user.IsAdmin = body.IsAdmin.Value;
            }

            if (body.IsTenantAdmin.HasValue && body.IsTenantAdmin.Value)
            {
                EnsureCanChangeTenantAdminFlag(requestContext, tenantId);
                user.IsTenantAdmin = body.IsTenantAdmin.Value;
            }

            if (body.Active.HasValue) user.Active = body.Active.Value;
            _Authentication.SetUserPassword(user, password);
            return user;
        }

        private Credential CreateCredential(RequestContext requestContext, HttpContextBase context)
        {
            XenoPlatform platform = RequirePlatform();
            CredentialSaveRequest body = ReadJsonBody<CredentialSaveRequest>(context);
            if (!String.IsNullOrWhiteSpace(body.SecretKey)) throw new ArgumentException("Secret keys are not accepted for credential creation.");
            string tenantId = FirstNonEmpty(body.TenantId, requestContext.TenantId);
            if (!IsTenantVisible(requestContext, tenantId)) throw new AuthorizationDeniedException(403, "TenantMismatch", "Authenticated tenant cannot access the requested tenant.");
            string userId = FirstNonEmpty(body.UserId, requestContext.UserId);
            if (String.IsNullOrWhiteSpace(userId))
            {
                User? user = platform.Users.FirstOrDefault(item => item.TenantId == tenantId && item.Active);
                userId = user?.Id ?? String.Empty;
            }

            if (String.IsNullOrWhiteSpace(userId)) throw new ArgumentException("User ID is required.");
            if (!platform.Users.Any(item => item.Id == userId && item.TenantId == tenantId && item.Active)) throw new ArgumentException("Credential user does not exist or is inactive.");
            if (!CanManageTenant(requestContext, tenantId) && !String.Equals(userId, requestContext.UserId, StringComparison.Ordinal))
            {
                throw new AuthorizationDeniedException(403, "SelfServiceCredentialOnly", "Regular users can create credentials only for their own account.");
            }

            string accessKey = FirstNonEmpty(body.AccessKey, IdGenerator.NewId("xeno_"));
            if (platform.Credentials.Any(item => item.TenantId == tenantId && String.Equals(item.AccessKey, accessKey, StringComparison.Ordinal))) throw new ArgumentException("Access key already exists in the tenant.");
            Credential credential = new Credential
            {
                TenantId = tenantId,
                UserId = userId,
                AccessKey = accessKey,
                SecretKey = _Authentication.HashCredentialSecret(_Authentication.GenerateSecret()),
                Active = true
            };
            if (body.Active.HasValue) credential.Active = body.Active.Value;
            return credential;
        }

        private Role CreateRole(RequestContext requestContext, HttpContextBase context)
        {
            XenoPlatform platform = RequirePlatform();
            RoleSaveRequest body = ReadJsonBody<RoleSaveRequest>(context);
            string name = FirstNonEmpty(body.Name);
            if (String.IsNullOrWhiteSpace(name)) throw new ArgumentException("Role name is required.");
            if (platform.Roles.Any(item => item.TenantId == requestContext.TenantId && String.Equals(item.Name, name, StringComparison.OrdinalIgnoreCase))) throw new ArgumentException("Role name already exists in the tenant.");

            Role role = new Role
            {
                TenantId = requestContext.TenantId,
                Name = name.Trim(),
                BuiltIn = false,
                PermissionIds = GetGrantablePermissionIds(requestContext, GetStringSet(body.PermissionIds)).ToList(),
                CreatedUtc = DateTime.UtcNow,
                LastUpdateUtc = DateTime.UtcNow,
                Active = true
            };
            if (body.Active.HasValue) role.Active = body.Active.Value;
            return role;
        }

        private Permission CreatePermission(RequestContext requestContext, HttpContextBase context)
        {
            PermissionSaveRequest body = ReadJsonBody<PermissionSaveRequest>(context);
            string resourceType = FirstNonEmpty(body.ResourceType);
            if (String.IsNullOrWhiteSpace(resourceType)) throw new ArgumentException("Resource type is required.");

            OperationTypeEnum operationType = OperationTypeEnum.Read;
            string? operationTypeText = body.OperationType;
            if (!String.IsNullOrWhiteSpace(operationTypeText) && !Enum.TryParse(operationTypeText, true, out operationType)) throw new ArgumentException("Operation type is invalid.");
            EnsureCanGrantPermission(requestContext, requestContext.TenantId, resourceType, operationType);

            Permission permission = new Permission
            {
                TenantId = requestContext.TenantId,
                ResourceType = resourceType.Trim(),
                OperationType = operationType,
                Permit = true,
                CreatedUtc = DateTime.UtcNow,
                LastUpdateUtc = DateTime.UtcNow,
                Active = true
            };
            if (body.Permit.HasValue) permission.Permit = body.Permit.Value;
            if (body.Active.HasValue) permission.Active = body.Active.Value;
            return permission;
        }

        private IEnumerable<string> GetGrantablePermissionIds(RequestContext requestContext, IEnumerable<string> permissionIds)
        {
            XenoPlatform platform = RequirePlatform();
            foreach (string permissionId in permissionIds)
            {
                Permission? permission = platform.Permissions.FirstOrDefault(item => item.Id == permissionId && item.TenantId == requestContext.TenantId && item.Active);
                if (permission == null) continue;
                EnsureCanGrantPermission(requestContext, permission.TenantId, permission.ResourceType, permission.OperationType);
                yield return permission.Id;
            }
        }

        private List<Permission> ListGrantablePermissions(RequestContext requestContext)
        {
            XenoPlatform platform = RequirePlatform();
            List<Permission> permissions = platform.Permissions
                .Where(item => item.TenantId == requestContext.TenantId && item.Active)
                .ToList();
            if (_Authorization.IsSystemAdmin(platform, requestContext)) return permissions;
            if (_Authorization.IsTenantAdmin(platform, requestContext, requestContext.TenantId)) return permissions;
            return permissions
                .Where(permission => _Authorization.Authorize(platform, new AuthorizationRequest
                {
                    Context = requestContext,
                    TargetTenantId = permission.TenantId,
                    ResourceType = permission.ResourceType,
                    OperationType = permission.OperationType
                }).Allowed)
                .ToList();
        }

        private void EnsureCanGrantPermission(RequestContext requestContext, string tenantId, string resourceType, OperationTypeEnum operationType)
        {
            if (_Authorization.IsSystemAdmin(RequirePlatform(), requestContext)) return;
            if (_Authorization.IsTenantAdmin(RequirePlatform(), requestContext, tenantId)) return;
            AuthorizationDecision decision = _Authorization.Authorize(RequirePlatform(), new AuthorizationRequest
            {
                Context = requestContext,
                TargetTenantId = tenantId,
                ResourceType = resourceType,
                OperationType = operationType
            });
            if (decision.Allowed) return;
            _Authentication.AppendAudit(RequirePlatform(), requestContext, resourceType, operationType, false, "GrantExceedsAuthority", 403, tenantId, null);
            throw new AuthorizationDeniedException(403, "GrantExceedsAuthority", "Permission grant exceeds the grantor's authority.");
        }

        private RoutingPolicy CreateRoutingPolicy(RequestContext requestContext, HttpContextBase context)
        {
            XenoPlatform platform = RequirePlatform();
            RoutingPolicySaveRequest body = ReadJsonBody<RoutingPolicySaveRequest>(context);
            string tenantId = FirstNonEmpty(body.TenantId, requestContext.TenantId);
            if (!IsTenantVisible(requestContext, tenantId)) throw new AuthorizationDeniedException(403, "TenantMismatch", "Authenticated tenant cannot access the requested tenant.");
            if (!platform.Tenants.Any(item => item.Id == tenantId && item.Active)) throw new ArgumentException("Tenant does not exist or is inactive.");

            string name = FirstNonEmpty(body.Name);
            if (String.IsNullOrWhiteSpace(name)) throw new ArgumentException("Routing policy name is required.");

            RoutingPolicy policy = new RoutingPolicy
            {
                TenantId = tenantId,
                Name = name.Trim(),
                ModelName = NormalizePolicyMatch(body.ModelName),
                ClientName = NormalizePolicyMatch(body.ClientName),
                RequestTimeoutMs = NormalizePolicyTimeout(body.RequestTimeoutMs),
                LoadBalancingMode = body.LoadBalancingMode ?? LoadBalancingModeEnum.LeastRecentlyUsed,
                Adaptive = body.Adaptive ?? new AdaptiveLoadBalancingSettings(),
                MaxTelemetryAgeMs = body.MaxTelemetryAgeMs ?? 30000,
                TelemetryFilters = body.TelemetryFilters ?? new List<RoutingPolicyCondition>(),
                TelemetryRanking = body.TelemetryRanking ?? new List<RoutingPolicyRankingRule>(),
                TelemetryFallbackMode = body.TelemetryFallbackMode ?? RoutingPolicyFallbackModeEnum.UseLoadBalancingMode,
                IsDefault = false,
                Active = body.Active ?? true,
                CreatedUtc = DateTime.UtcNow,
                LastUpdateUtc = DateTime.UtcNow
            };

            EnsureRoutingPolicyUnique(policy, null);
            return policy;
        }

        private RoutingPolicy UpdateRoutingPolicy(RequestContext requestContext, string policyId, HttpContextBase context)
        {
            RoutingPolicy? policy = RequirePlatform().RoutingPolicies.FirstOrDefault(item => item.Id == policyId && item.TenantId == requestContext.TenantId);
            if (policy == null) throw new KeyNotFoundException("Routing policy not found.");

            RoutingPolicySaveRequest body = ReadJsonBody<RoutingPolicySaveRequest>(context);
            string? name = body.Name;
            if (name != null)
            {
                if (String.IsNullOrWhiteSpace(name)) throw new ArgumentException("Routing policy name is required.");
                policy.Name = name.Trim();
            }

            if (!policy.IsDefault)
            {
                if (body.ModelName != null) policy.ModelName = NormalizePolicyMatch(body.ModelName);
                if (body.ClientName != null) policy.ClientName = NormalizePolicyMatch(body.ClientName);
                if (body.Active.HasValue) policy.Active = body.Active.Value;
            }
            else
            {
                policy.ModelName = "*";
                policy.ClientName = "*";
                policy.Active = true;
            }

            if (body.RequestTimeoutMs.HasValue) policy.RequestTimeoutMs = NormalizePolicyTimeout(body.RequestTimeoutMs);
            if (body.LoadBalancingMode.HasValue) policy.LoadBalancingMode = body.LoadBalancingMode.Value;
            if (body.Adaptive != null) policy.Adaptive = body.Adaptive;
            if (body.MaxTelemetryAgeMs.HasValue) policy.MaxTelemetryAgeMs = body.MaxTelemetryAgeMs.Value;
            if (body.TelemetryFilters != null) policy.TelemetryFilters = body.TelemetryFilters;
            if (body.TelemetryRanking != null) policy.TelemetryRanking = body.TelemetryRanking;
            if (body.TelemetryFallbackMode.HasValue) policy.TelemetryFallbackMode = body.TelemetryFallbackMode.Value;
            policy.LastUpdateUtc = DateTime.UtcNow;
            EnsureRoutingPolicyUnique(policy, policy.Id);
            return policy;
        }

        private int DeleteRoutingPolicy(RequestContext requestContext, string policyId)
        {
            RoutingPolicy? policy = RequirePlatform().RoutingPolicies.FirstOrDefault(item => item.Id == policyId && item.TenantId == requestContext.TenantId);
            if (policy == null) return 0;
            if (policy.IsDefault) throw new InvalidOperationException("The default routing policy cannot be deleted.");
            return RequirePlatform().RoutingPolicies.RemoveAll(item => item.Id == policyId && item.TenantId == requestContext.TenantId);
        }

        private async Task<AdminNukeResponse> HandleNukeTenantAsync(HttpContextBase context, RequestContext requestContext, CancellationToken token)
        {
            token.ThrowIfCancellationRequested();
            AdminNukeTenantRequest body = ReadJsonBody<AdminNukeTenantRequest>(context);
            string tenantId = FirstNonEmpty(body.TenantId).Trim();
            string confirmTenantId = FirstNonEmpty(body.ConfirmTenantId).Trim();
            ValidateNukeReason(body.Reason);
            if (String.IsNullOrWhiteSpace(tenantId)) throw new ArgumentException("tenantId is required.");
            if (!String.Equals(tenantId, confirmTenantId, StringComparison.Ordinal)) throw new ArgumentException("confirmTenantId must exactly match tenantId.");
            EnsureSystemAdmin(requestContext, tenantId, tenantId);
            if (String.Equals(tenantId, Constants.DefaultTenantId, StringComparison.OrdinalIgnoreCase)) throw new HttpStatusException(409, "ProtectedTenant", "The default/system tenant cannot be nuked.");

            XenoPlatform platform = RequirePlatform();
            Tenant? tenant = platform.Tenants.FirstOrDefault(item => String.Equals(item.Id, tenantId, StringComparison.OrdinalIgnoreCase));
            if (tenant == null) throw new HttpStatusException(404, "TenantNotFound", "Tenant was not found.");

            DateTime startedUtc = DateTime.UtcNow;
            AdminNukeCounts counts = platform.CountTenantScopedRecords(tenantId, body.IncludeAuditRecords, body.IncludeRequestHistory);
            AdminNukeResponse response = new AdminNukeResponse
            {
                Status = body.DryRun ? "dryRun" : "success",
                OperationId = IdGenerator.NewId("nuke_"),
                OperationType = "TenantNuke",
                DryRun = body.DryRun,
                Tenant = new AdminNukeTenantDetails
                {
                    TenantId = tenant.Id,
                    Name = tenant.Name,
                    Status = body.DryRun ? (tenant.Active ? "Active" : "Inactive") : "Deleted"
                },
                RequestedBy = BuildNukeRequestedBy(requestContext, tenantId),
                Deleted = counts,
                StartedUtc = startedUtc
            };

            if (body.DryRun)
            {
                response.Warnings.Add("Dry run only. No data was deleted.");
                _Authentication.AppendAudit(platform, requestContext, "Admin.Nuke.Tenant", OperationTypeEnum.Admin, true, "TenantNukeDryRun", 200, tenantId, tenantId);
                await SavePlatformAsync(token).ConfigureAwait(false);
                response.CompletedUtc = DateTime.UtcNow;
                return response;
            }

            string nukeKey = "tenant:" + tenantId;
            if (!_ActiveNukeOperations.TryAdd(nukeKey, 0)) throw new HttpStatusException(409, "NukeAlreadyRunning", "A nuke operation is already running for this tenant.");
            try
            {
                List<string> clientIds = platform.Clients
                    .Where(item => String.Equals(item.TenantId, tenantId, StringComparison.OrdinalIgnoreCase))
                    .Select(item => item.Id)
                    .ToList();
                response.Deleted = platform.RemoveTenantScopedRecords(tenantId, body.IncludeAuditRecords, body.IncludeRequestHistory);
                if (body.IncludeTelemetry && _Database != null)
                {
                    response.Deleted.TelemetryRecords = await _Database.PurgeTenantDataAsync(tenantId, token).ConfigureAwait(false);
                }

                _TelemetryLatestCache.ClearTenant(tenantId);
                RemoveClientConnections(clientIds);
                if (body.IncludeRequestHistory && platform.CountTenantRemnants(tenantId) > 0)
                {
                    throw new HttpStatusException(500, "TenantNukeRemnantsDetected", "Tenant nuke left tenant-scoped remnants.");
                }

                _Authentication.AppendAudit(platform, requestContext, "Admin.Nuke.Tenant", OperationTypeEnum.Admin, true, "TenantNukeExecuted", 200, tenantId, tenantId);
                await SavePlatformAsync(token).ConfigureAwait(false);
                response.CompletedUtc = DateTime.UtcNow;
                return response;
            }
            finally
            {
                _ActiveNukeOperations.TryRemove(nukeKey, out _);
            }
        }

        private async Task<AdminNukeResponse> HandleNukeUserAsync(HttpContextBase context, RequestContext requestContext, CancellationToken token)
        {
            token.ThrowIfCancellationRequested();
            AdminNukeUserRequest body = ReadJsonBody<AdminNukeUserRequest>(context);
            string tenantId = FirstNonEmpty(body.TenantId).Trim();
            string userId = FirstNonEmpty(body.UserId).Trim();
            string confirmUserId = FirstNonEmpty(body.ConfirmUserId).Trim();
            ValidateNukeReason(body.Reason);
            if (String.IsNullOrWhiteSpace(tenantId)) throw new ArgumentException("tenantId is required.");
            if (String.IsNullOrWhiteSpace(userId)) throw new ArgumentException("userId is required.");
            if (!String.Equals(userId, confirmUserId, StringComparison.Ordinal)) throw new ArgumentException("confirmUserId must exactly match userId.");

            XenoPlatform platform = RequirePlatform();
            bool isSystemAdmin = _Authorization.IsSystemAdmin(platform, requestContext);
            bool isTenantAdmin = _Authorization.IsTenantAdmin(platform, requestContext, tenantId);
            if (!requestContext.IsAuthenticated) throw new UnauthorizedAccessException("Authentication is required.");
            if (!isSystemAdmin && !isTenantAdmin)
            {
                _Authentication.AppendAudit(platform, requestContext, "Admin.Nuke.User", OperationTypeEnum.Admin, false, "TenantAdminRequired", 403, tenantId, userId);
                await SavePlatformAsync(token).ConfigureAwait(false);
                throw new AuthorizationDeniedException(403, "TenantAdminRequired", "Only a system administrator or same-tenant tenant administrator can nuke a user.");
            }

            Tenant? tenant = platform.Tenants.FirstOrDefault(item => String.Equals(item.Id, tenantId, StringComparison.OrdinalIgnoreCase));
            if (tenant == null) throw new HttpStatusException(404, "TenantNotFound", "Tenant was not found.");
            User? user = platform.Users.FirstOrDefault(item => String.Equals(item.TenantId, tenantId, StringComparison.OrdinalIgnoreCase) && String.Equals(item.Id, userId, StringComparison.OrdinalIgnoreCase));
            if (user == null) throw new HttpStatusException(404, "UserNotFound", "User was not found.");
            if (!isSystemAdmin && String.Equals(requestContext.UserId, userId, StringComparison.OrdinalIgnoreCase)) throw new HttpStatusException(409, "TenantAdminSelfNukeProtected", "Tenant administrators cannot nuke themselves.");
            if (!isSystemAdmin && user.IsAdmin) throw new AuthorizationDeniedException(403, "SystemAdminProtected", "Tenant administrators cannot nuke global administrators.");
            if (user.IsAdmin && platform.Users.Count(item => item.Active && item.IsAdmin) <= 1) throw new HttpStatusException(409, "LastSystemAdminProtected", "The last active system administrator cannot be nuked.");

            DateTime startedUtc = DateTime.UtcNow;
            AdminNukeResponse counted = platform.CountUserScopedRecords(tenantId, userId, body.IncludeAuditRecords, body.AnonymizeRequestHistory);
            AdminNukeResponse response = new AdminNukeResponse
            {
                Status = body.DryRun ? "dryRun" : "success",
                OperationId = IdGenerator.NewId("nuke_"),
                OperationType = "UserNuke",
                DryRun = body.DryRun,
                Tenant = new AdminNukeTenantDetails
                {
                    TenantId = tenant.Id,
                    Name = tenant.Name,
                    Status = tenant.Active ? "Active" : "Inactive"
                },
                User = new AdminNukeUserDetails
                {
                    UserId = user.Id,
                    Email = user.Email,
                    Status = body.DryRun ? (user.Active ? "Active" : "Inactive") : "Deleted"
                },
                RequestedBy = BuildNukeRequestedBy(requestContext, tenantId),
                Deleted = counted.Deleted,
                Updated = counted.Updated,
                StartedUtc = startedUtc
            };

            if (body.DryRun)
            {
                response.Warnings.Add("Dry run only. No data was deleted.");
                _Authentication.AppendAudit(platform, requestContext, "Admin.Nuke.User", OperationTypeEnum.Admin, true, "UserNukeDryRun", 200, tenantId, userId);
                await SavePlatformAsync(token).ConfigureAwait(false);
                response.CompletedUtc = DateTime.UtcNow;
                return response;
            }

            string nukeKey = "user:" + tenantId + ":" + userId;
            if (!_ActiveNukeOperations.TryAdd(nukeKey, 0)) throw new HttpStatusException(409, "NukeAlreadyRunning", "A nuke operation is already running for this user.");
            try
            {
                AdminNukeResponse removed = platform.RemoveUserScopedRecords(tenantId, userId, body.IncludeAuditRecords, body.AnonymizeRequestHistory);
                response.Deleted = removed.Deleted;
                response.Updated = removed.Updated;
                if (body.AnonymizeRequestHistory && platform.CountUserRemnants(tenantId, userId) > 0)
                {
                    throw new HttpStatusException(500, "UserNukeRemnantsDetected", "User nuke left user-scoped remnants.");
                }

                _Authentication.AppendAudit(platform, requestContext, "Admin.Nuke.User", OperationTypeEnum.Admin, true, "UserNukeExecuted", 200, tenantId, userId);
                await SavePlatformAsync(token).ConfigureAwait(false);
                response.CompletedUtc = DateTime.UtcNow;
                return response;
            }
            finally
            {
                _ActiveNukeOperations.TryRemove(nukeKey, out _);
            }
        }

        private async Task<int> DeleteTenantAsync(string tenantId, RequestContext requestContext, CancellationToken token)
        {
            XenoPlatform platform = RequirePlatform();
            Tenant? tenant = platform.Tenants.FirstOrDefault(item => item.Id == tenantId);
            if (tenant == null) return 0;
            await RequireTenantLifecycle().DeleteAsync(tenant, true, requestContext, token).ConfigureAwait(false);

            List<string> clientIds = platform.Clients.Where(item => item.TenantId == tenantId).Select(item => item.Id).ToList();
            int removed = platform.Tenants.RemoveAll(item => item.Id == tenantId);
            if (removed > 0)
            {
                removed += platform.RemoveTenantScopedRecords(tenantId);
                await SavePlatformAsync(token).ConfigureAwait(false);
            }

            if (_Database != null) removed += await _Database.PurgeTenantDataAsync(tenantId, token).ConfigureAwait(false);
            _TelemetryLatestCache.ClearTenant(tenantId);
            RemoveClientConnections(clientIds);

            return removed;
        }

        private void EnsureRoutingPolicyUnique(RoutingPolicy policy, string? existingPolicyId)
        {
            bool duplicate = RequirePlatform().RoutingPolicies.Any(item =>
                item.TenantId == policy.TenantId &&
                item.Id != existingPolicyId &&
                String.Equals(item.ModelName, policy.ModelName, StringComparison.OrdinalIgnoreCase) &&
                String.Equals(item.ClientName, policy.ClientName, StringComparison.OrdinalIgnoreCase));
            if (duplicate) throw new ArgumentException("A routing policy with the same model and client match already exists in the tenant.");
        }

        private static string NormalizePolicyMatch(string? value)
        {
            if (String.IsNullOrWhiteSpace(value)) return "*";
            return value.Trim();
        }

        private static int NormalizePolicyTimeout(int? value)
        {
            int timeout = value ?? 300000;
            if (timeout < 1000) return 1000;
            if (timeout > 3600000) return 3600000;
            return timeout;
        }

        private Tenant UpdateTenant(string tenantId, TenantSaveRequest body)
        {
            Tenant? tenant = RequirePlatform().Tenants.FirstOrDefault(item => item.Id == tenantId);
            if (tenant == null) throw new KeyNotFoundException("Tenant not found.");

            string? name = body.Name;
            if (name != null)
            {
                if (String.IsNullOrWhiteSpace(name)) throw new ArgumentException("Tenant name is required.");
                tenant.Name = name.Trim();
            }

            if (body.Active.HasValue) tenant.Active = body.Active.Value;
            tenant.LastUpdateUtc = DateTime.UtcNow;
            return tenant;
        }

        private int DeleteUser(RequestContext requestContext, string userId)
        {
            XenoPlatform platform = RequirePlatform();
            User? user = platform.Users.FirstOrDefault(item => item.Id == userId && IsTenantVisible(requestContext, item.TenantId));
            if (user == null) return 0;

            List<string> credentialIds = platform.Credentials
                .Where(item => item.TenantId == user.TenantId && item.UserId == userId)
                .Select(item => item.Id)
                .ToList();

            int removed = platform.Users.RemoveAll(item => item.Id == userId && item.TenantId == user.TenantId);
            removed += platform.Credentials.RemoveAll(item => item.TenantId == user.TenantId && item.UserId == userId);
            removed += platform.AuthSessions.RemoveAll(item => item.TenantId == user.TenantId && (item.UserId == userId || (item.CredentialId != null && credentialIds.Contains(item.CredentialId))));
            removed += platform.RoleAssignments.RemoveAll(item => item.TenantId == user.TenantId && item.PrincipalType == PrincipalTypeEnum.User && item.PrincipalId == userId);
            removed += platform.RoleAssignments.RemoveAll(item => item.TenantId == user.TenantId && item.PrincipalType == PrincipalTypeEnum.Credential && credentialIds.Contains(item.PrincipalId));
            platform.LedgerAccountLinks.RemoveAll(item => item.XenoTenantId == user.TenantId && item.XenoUserId == userId);
            return removed;
        }

        private User UpdateUser(RequestContext requestContext, string userId, HttpContextBase context)
        {
            XenoPlatform platform = RequirePlatform();
            User? user = platform.Users.FirstOrDefault(item => item.Id == userId && IsTenantVisible(requestContext, item.TenantId));
            if (user == null) throw new KeyNotFoundException("User not found.");

            UserSaveRequest body = ReadJsonBody<UserSaveRequest>(context);
            bool selfService = !CanManageTenant(requestContext, user.TenantId);
            if (selfService && !String.Equals(user.Id, requestContext.UserId, StringComparison.Ordinal))
            {
                throw new AuthorizationDeniedException(403, "SelfServiceUserOnly", "Regular users can update only their own account.");
            }

            if (selfService && (body.IsAdmin.HasValue || body.IsTenantAdmin.HasValue || body.Active.HasValue))
            {
                throw new AuthorizationDeniedException(403, "SelfServiceUserFieldDenied", "Regular users cannot change administrative or account-state fields.");
            }

            string? username = body.Username;
            if (username != null)
            {
                if (String.IsNullOrWhiteSpace(username)) throw new ArgumentException("Username is required.");
                string normalizedUsername = username.Trim();
                if (platform.Users.Any(item => item.TenantId == user.TenantId && item.Id != user.Id && String.Equals(item.Username, normalizedUsername, StringComparison.OrdinalIgnoreCase)))
                {
                    throw new HttpStatusException(409, "UsernameAlreadyExists", "Username already exists in the tenant.");
                }

                user.Username = normalizedUsername;
            }

            string? email = body.Email;
            if (email != null)
            {
                string normalizedEmail = email.Trim();
                if (String.IsNullOrWhiteSpace(normalizedEmail)) throw new ArgumentException("Email is required.");
                if (!IsValidEmailAddress(normalizedEmail)) throw new ArgumentException("Email address is invalid.");
                if (platform.Users.Any(item => item.TenantId == user.TenantId && item.Id != user.Id && String.Equals(item.Email, normalizedEmail, StringComparison.OrdinalIgnoreCase)))
                {
                    throw new HttpStatusException(409, "EmailAlreadyExists", "Email already exists in the tenant.");
                }

                user.Email = normalizedEmail;
            }

            if (body.IsAdmin.HasValue && body.IsAdmin.Value != user.IsAdmin)
            {
                EnsureCanChangeSystemAdminFlag(requestContext);
                EnsureCanDisableSystemAdminFlag(requestContext, user, body.IsAdmin.Value);
                user.IsAdmin = body.IsAdmin.Value;
            }

            if (body.IsTenantAdmin.HasValue && body.IsTenantAdmin.Value != user.IsTenantAdmin)
            {
                EnsureCanChangeTenantAdminFlag(requestContext, user.TenantId);
                user.IsTenantAdmin = body.IsTenantAdmin.Value;
            }

            if (body.Active.HasValue) user.Active = body.Active.Value;

            string? password = body.Password;
            if (!String.IsNullOrWhiteSpace(password)) _Authentication.SetUserPassword(user, password);

            return user;
        }

        private void EnsureCanChangeSystemAdminFlag(RequestContext requestContext)
        {
            if (_Authorization.IsSystemAdmin(RequirePlatform(), requestContext)) return;
            _Authentication.AppendAudit(RequirePlatform(), requestContext, "Users.SystemAdmin", OperationTypeEnum.Admin, false, "SystemAdminRequired", 403, requestContext.TenantId, null);
            throw new AuthorizationDeniedException(403, "SystemAdminRequired", "Only a system administrator can change the system administrator flag.");
        }

        private void EnsureCanDisableSystemAdminFlag(RequestContext requestContext, User user, bool requestedIsAdmin)
        {
            if (requestedIsAdmin) return;
            if (!user.IsAdmin) return;
            if (!String.Equals(requestContext.UserId, user.Id, StringComparison.Ordinal)) return;

            int activeSystemAdminCount = RequirePlatform().Users.Count(item => item.Active && item.IsAdmin);
            if (activeSystemAdminCount > 1) return;

            _Authentication.AppendAudit(RequirePlatform(), requestContext, "Users.SystemAdmin", OperationTypeEnum.Admin, false, "LastSystemAdminProtected", 403, user.TenantId, user.Id);
            throw new AuthorizationDeniedException(403, "LastSystemAdminProtected", "The last active system administrator cannot remove their own system administrator flag.");
        }

        private void EnsureSystemAdmin(RequestContext requestContext, string targetTenantId, string? targetResourceId)
        {
            if (!requestContext.IsAuthenticated) throw new UnauthorizedAccessException("Authentication is required.");
            if (_Authorization.IsSystemAdmin(RequirePlatform(), requestContext)) return;
            _Authentication.AppendAudit(RequirePlatform(), requestContext, "Admin.Nuke", OperationTypeEnum.Admin, false, "SystemAdminRequired", 403, targetTenantId, targetResourceId);
            throw new AuthorizationDeniedException(403, "SystemAdminRequired", "Only a system administrator can perform this operation.");
        }

        private AdminNukeRequestedBy BuildNukeRequestedBy(RequestContext requestContext, string targetTenantId)
        {
            XenoPlatform platform = RequirePlatform();
            return new AdminNukeRequestedBy
            {
                TenantId = requestContext.TenantId,
                UserId = requestContext.UserId,
                IsGlobalAdmin = _Authorization.IsSystemAdmin(platform, requestContext),
                IsTenantAdmin = _Authorization.IsTenantAdmin(platform, requestContext, targetTenantId)
            };
        }

        private static string ValidateNukeReason(string? reason)
        {
            string normalized = FirstNonEmpty(reason).Trim();
            if (normalized.Length < 10) throw new ArgumentException("reason must be at least 10 characters.");
            if (normalized.Length > 2048) throw new ArgumentException("reason must be 2048 characters or fewer.");
            return normalized;
        }

        private void RemoveClientConnections(List<string> clientIds)
        {
            lock (_ClientConnectionsLock)
            {
                foreach (string clientId in clientIds) _ClientConnections.Remove(clientId);
            }
        }

        private void EnsureCanChangeTenantAdminFlag(RequestContext requestContext, string tenantId)
        {
            if (_Authorization.IsSystemAdmin(RequirePlatform(), requestContext)) return;
            if (_Authorization.IsTenantAdmin(RequirePlatform(), requestContext, tenantId)) return;
            _Authentication.AppendAudit(RequirePlatform(), requestContext, "Users.TenantAdmin", OperationTypeEnum.Admin, false, "TenantAdminRequired", 403, tenantId, null);
            throw new AuthorizationDeniedException(403, "TenantAdminRequired", "Only a system administrator or same-tenant tenant administrator can change the tenant administrator flag.");
        }

        private Credential UpdateCredential(RequestContext requestContext, string credentialId, HttpContextBase context)
        {
            Credential? credential = RequirePlatform().Credentials.FirstOrDefault(item => item.Id == credentialId && item.TenantId == requestContext.TenantId);
            if (credential == null) throw new KeyNotFoundException("Credential not found.");
            if (!CanManageTenant(requestContext, credential.TenantId) && !String.Equals(credential.UserId, requestContext.UserId, StringComparison.Ordinal))
            {
                throw new AuthorizationDeniedException(403, "SelfServiceCredentialOnly", "Regular users can update only their own credentials.");
            }

            CredentialSaveRequest body = ReadJsonBody<CredentialSaveRequest>(context);
            string? userId = body.UserId;
            if (userId != null)
            {
                if (!CanManageTenant(requestContext, credential.TenantId)) throw new AuthorizationDeniedException(403, "SelfServiceCredentialOwnerDenied", "Regular users cannot reassign credentials.");
                credential.UserId = userId.Trim();
            }

            string? accessKey = body.AccessKey;
            if (accessKey != null)
            {
                if (String.IsNullOrWhiteSpace(accessKey)) throw new ArgumentException("Access key is required.");
                string normalizedAccessKey = accessKey.Trim();
                if (RequirePlatform().Credentials.Any(item => item.TenantId == credential.TenantId && item.Id != credential.Id && String.Equals(item.AccessKey, normalizedAccessKey, StringComparison.Ordinal)))
                {
                    throw new HttpStatusException(409, "AccessKeyAlreadyExists", "Access key already exists in the tenant.");
                }

                credential.AccessKey = normalizedAccessKey;
            }

            if (!String.IsNullOrWhiteSpace(body.SecretKey)) throw new ArgumentException("Secret keys are not accepted for credential updates.");
            if (body.Active.HasValue) credential.Active = body.Active.Value;
            return credential;
        }

        private int DeleteCredential(RequestContext requestContext, string credentialId)
        {
            XenoPlatform platform = RequirePlatform();
            Credential? credential = platform.Credentials.FirstOrDefault(item => item.Id == credentialId && item.TenantId == requestContext.TenantId);
            if (credential == null) return 0;

            int removed = platform.Credentials.RemoveAll(item => item.Id == credentialId && item.TenantId == requestContext.TenantId);
            removed += platform.AuthSessions.RemoveAll(item => item.CredentialId == credentialId && item.TenantId == requestContext.TenantId);
            removed += platform.RoleAssignments.RemoveAll(item => item.PrincipalType == PrincipalTypeEnum.Credential && item.PrincipalId == credentialId && item.TenantId == requestContext.TenantId);
            return removed;
        }

        private Role UpdateRole(RequestContext requestContext, string roleId, HttpContextBase context)
        {
            Role? role = RequirePlatform().Roles.FirstOrDefault(item => item.Id == roleId && item.TenantId == requestContext.TenantId);
            if (role == null) throw new KeyNotFoundException("Role not found.");
            if (role.BuiltIn) throw new AuthorizationDeniedException(403, "BuiltInRoleProtected", "Built-in roles cannot be modified through the management API.");

            RoleSaveRequest body = ReadJsonBody<RoleSaveRequest>(context);
            string? name = body.Name;
            if (name != null)
            {
                if (String.IsNullOrWhiteSpace(name)) throw new ArgumentException("Role name is required.");
                role.Name = name.Trim();
            }

            if (body.PermissionIds != null) role.PermissionIds = GetGrantablePermissionIds(requestContext, GetStringSet(body.PermissionIds)).ToList();
            if (body.Active.HasValue) role.Active = body.Active.Value;
            role.LastUpdateUtc = DateTime.UtcNow;
            return role;
        }

        private int DeleteRole(RequestContext requestContext, string roleId)
        {
            XenoPlatform platform = RequirePlatform();
            Role? role = platform.Roles.FirstOrDefault(item => item.Id == roleId && item.TenantId == requestContext.TenantId);
            if (role != null && role.BuiltIn) throw new AuthorizationDeniedException(403, "BuiltInRoleProtected", "Built-in roles cannot be deleted through the management API.");
            int removed = platform.Roles.RemoveAll(item => item.Id == roleId && item.TenantId == requestContext.TenantId);
            removed += platform.RoleAssignments.RemoveAll(item => item.RoleId == roleId && item.TenantId == requestContext.TenantId);
            return removed;
        }

        private Permission UpdatePermission(RequestContext requestContext, string permissionId, HttpContextBase context)
        {
            Permission? permission = RequirePlatform().Permissions.FirstOrDefault(item => item.Id == permissionId && item.TenantId == requestContext.TenantId);
            if (permission == null) throw new KeyNotFoundException("Permission not found.");
            if (permission.BuiltIn) throw new AuthorizationDeniedException(403, "BuiltInPermissionProtected", "Built-in permissions cannot be modified through the management API.");

            PermissionSaveRequest body = ReadJsonBody<PermissionSaveRequest>(context);
            string? resourceType = body.ResourceType;
            if (resourceType != null)
            {
                if (String.IsNullOrWhiteSpace(resourceType)) throw new ArgumentException("Resource type is required.");
                permission.ResourceType = resourceType.Trim();
            }

            string? operationType = body.OperationType;
            if (!String.IsNullOrWhiteSpace(operationType))
            {
                if (!Enum.TryParse(operationType, true, out OperationTypeEnum parsedOperation)) throw new ArgumentException("Operation type is invalid.");
                EnsureCanGrantPermission(requestContext, requestContext.TenantId, permission.ResourceType, parsedOperation);
                permission.OperationType = parsedOperation;
            }

            EnsureCanGrantPermission(requestContext, requestContext.TenantId, permission.ResourceType, permission.OperationType);
            if (body.Permit.HasValue) permission.Permit = body.Permit.Value;
            if (body.Active.HasValue) permission.Active = body.Active.Value;
            permission.LastUpdateUtc = DateTime.UtcNow;
            return permission;
        }

        private int DeletePermission(RequestContext requestContext, string permissionId)
        {
            XenoPlatform platform = RequirePlatform();
            Permission? permission = platform.Permissions.FirstOrDefault(item => item.Id == permissionId && item.TenantId == requestContext.TenantId);
            if (permission != null && permission.BuiltIn) throw new AuthorizationDeniedException(403, "BuiltInPermissionProtected", "Built-in permissions cannot be deleted through the management API.");
            int removed = platform.Permissions.RemoveAll(item => item.Id == permissionId && item.TenantId == requestContext.TenantId);
            foreach (Role role in platform.Roles.Where(item => item.TenantId == requestContext.TenantId))
            {
                role.PermissionIds.RemoveAll(item => String.Equals(item, permissionId, StringComparison.OrdinalIgnoreCase));
            }

            return removed;
        }

        private RoleAssignment CreateRoleAssignment(RequestContext requestContext, HttpContextBase context)
        {
            XenoPlatform platform = RequirePlatform();
            RoleAssignmentSaveRequest body = ReadJsonBody<RoleAssignmentSaveRequest>(context);
            string tenantId = FirstNonEmpty(body.TenantId, requestContext.TenantId);
            if (!IsTenantVisible(requestContext, tenantId)) throw new AuthorizationDeniedException(403, "TenantMismatch", "Authenticated tenant cannot access the requested tenant.");

            string roleId = FirstNonEmpty(body.RoleId);
            if (String.IsNullOrWhiteSpace(roleId)) throw new ArgumentException("Role ID is required.");
            Role? role = platform.Roles.FirstOrDefault(item => item.Id == roleId && item.TenantId == tenantId && item.Active);
            if (role == null) throw new KeyNotFoundException("Role not found.");
            foreach (string permissionId in role.PermissionIds)
            {
                Permission? permission = platform.Permissions.FirstOrDefault(item => item.Id == permissionId && item.TenantId == tenantId && item.Active);
                if (permission == null) continue;
                EnsureCanGrantPermission(requestContext, tenantId, permission.ResourceType, permission.OperationType);
            }

            string principalTypeText = FirstNonEmpty(body.PrincipalType, PrincipalTypeEnum.User.ToString());
            if (!Enum.TryParse(principalTypeText, true, out PrincipalTypeEnum principalType)) throw new ArgumentException("Principal type is invalid.");
            if (principalType != PrincipalTypeEnum.User && principalType != PrincipalTypeEnum.Credential) throw new ArgumentException("Principal type must be User or Credential.");

            string principalId = FirstNonEmpty(body.PrincipalId);
            if (String.IsNullOrWhiteSpace(principalId)) throw new ArgumentException("Principal ID is required.");
            if (principalType == PrincipalTypeEnum.User && !platform.Users.Any(item => item.Id == principalId && item.TenantId == tenantId && item.Active)) throw new ArgumentException("User principal does not exist in the tenant.");
            if (principalType == PrincipalTypeEnum.Credential && !platform.Credentials.Any(item => item.Id == principalId && item.TenantId == tenantId && item.Active)) throw new ArgumentException("Credential principal does not exist in the tenant.");

            RoleAssignment? existing = platform.RoleAssignments.FirstOrDefault(item =>
                item.TenantId == tenantId &&
                item.RoleId == roleId &&
                item.PrincipalType == principalType &&
                item.PrincipalId == principalId);
            if (existing != null)
            {
                throw new ArgumentException("Role assignment already exists.");
            }

            RoleAssignment assignment = new RoleAssignment
            {
                TenantId = tenantId,
                RoleId = roleId,
                PrincipalType = principalType,
                PrincipalId = principalId,
                Active = body.Active ?? true,
                CreatedUtc = DateTime.UtcNow,
                LastUpdateUtc = DateTime.UtcNow
            };
            return assignment;
        }

        private int DeleteRoleAssignment(RequestContext requestContext, string assignmentId)
        {
            XenoPlatform platform = RequirePlatform();
            RoleAssignment? assignment = platform.RoleAssignments.FirstOrDefault(item => item.Id == assignmentId && IsTenantVisible(requestContext, item.TenantId));
            if (assignment == null) return 0;
            Role? role = platform.Roles.FirstOrDefault(item => item.Id == assignment.RoleId && item.TenantId == assignment.TenantId);
            if (role != null)
            {
                foreach (string permissionId in role.PermissionIds)
                {
                    Permission? permission = platform.Permissions.FirstOrDefault(item => item.Id == permissionId && item.TenantId == assignment.TenantId && item.Active);
                    if (permission == null) continue;
                    EnsureCanGrantPermission(requestContext, assignment.TenantId, permission.ResourceType, permission.OperationType);
                }
            }

            return platform.RoleAssignments.RemoveAll(item => item.Id == assignmentId && item.TenantId == assignment.TenantId);
        }

        private int DeleteClient(RequestContext requestContext, string clientId)
        {
            XenoPlatform platform = RequirePlatform();
            int removed = platform.Clients.RemoveAll(item => item.Id == clientId && item.TenantId == requestContext.TenantId);
            if (removed > 0)
            {
                lock (_ClientConnectionsLock)
                {
                    _ClientConnections.Remove(clientId);
                }
            }

            return removed;
        }

        private int DeleteClientModel(RequestContext requestContext, HttpContextBase context)
        {
            ClientModelDeleteRequest body = ReadJsonBody<ClientModelDeleteRequest>(context);
            string clientId = body.ClientId ?? String.Empty;
            string modelName = body.Name ?? String.Empty;
            string source = body.Source ?? String.Empty;
            if (String.IsNullOrWhiteSpace(clientId)) throw new ArgumentException("Client ID is required.");
            if (String.IsNullOrWhiteSpace(modelName)) throw new ArgumentException("Model name is required.");

            XenoClientRecord? client = RequirePlatform().Clients.FirstOrDefault(item => item.Id == clientId && item.TenantId == requestContext.TenantId);
            if (client == null) return 0;

            return client.Models.RemoveAll(item =>
                String.Equals(item.Name, modelName, StringComparison.OrdinalIgnoreCase)
                && (String.IsNullOrWhiteSpace(source) || String.Equals(item.Source, source, StringComparison.OrdinalIgnoreCase)));
        }

        private LedgerBalanceSearchRequest BuildLedgerBalanceSearchRequest(RequestContext requestContext, HttpContextBase context)
        {
            LedgerBalanceSearchRequest request = new LedgerBalanceSearchRequest
            {
                TenantId = DecodeQueryValue(context.Request.Query.Elements.Get("tenantId")),
                UserId = DecodeQueryValue(context.Request.Query.Elements.Get("userId")),
                Search = DecodeQueryValue(context.Request.Query.Elements.Get("search")),
                Page = ParsePositiveIntQuery(context, "page", 1, 100000),
                PageSize = ParsePositiveIntQuery(context, "pageSize", 100, 1000)
            };
            if (String.IsNullOrWhiteSpace(request.TenantId)) request.TenantId = requestContext.TenantId;
            return request;
        }

        private LedgerBalanceSearchResult SearchLedgerBalances(RequestContext requestContext, LedgerBalanceSearchRequest request)
        {
            if (request == null) throw new ArgumentNullException(nameof(request));
            string tenantId = String.IsNullOrWhiteSpace(request.TenantId) ? requestContext.TenantId : request.TenantId;
            if (!IsTenantVisible(requestContext, tenantId)) throw new AuthorizationDeniedException(403, "TenantMismatch", "Authenticated tenant cannot access the requested tenant.");

            int page = Math.Max(1, request.Page);
            int pageSize = Math.Clamp(request.PageSize, 1, 1000);
            IEnumerable<User> users = RequirePlatform().Users.Where(item => item.TenantId == tenantId && item.Active);
            if (!CanManageTenant(requestContext, tenantId))
            {
                string currentUserId = ResolveContextUserId(requestContext);
                users = users.Where(item => item.Id == currentUserId);
            }

            if (!String.IsNullOrWhiteSpace(request.UserId))
            {
                users = users.Where(item => item.Id == request.UserId);
            }

            if (!String.IsNullOrWhiteSpace(request.Search))
            {
                string search = request.Search;
                users = users.Where(item =>
                    item.Id.Contains(search, StringComparison.OrdinalIgnoreCase)
                    || item.Username.Contains(search, StringComparison.OrdinalIgnoreCase)
                    || item.Email.Contains(search, StringComparison.OrdinalIgnoreCase));
            }

            List<LedgerBalanceResponse> balances = users
                .OrderBy(item => item.Email, StringComparer.OrdinalIgnoreCase)
                .Select(item => BuildLedgerBalance(tenantId, item.Id))
                .ToList();

            return new LedgerBalanceSearchResult
            {
                Total = balances.Count,
                Page = page,
                PageSize = pageSize,
                Items = balances.Skip((page - 1) * pageSize).Take(pageSize).ToList()
            };
        }

        private LedgerBalanceResponse BuildLedgerBalance(string tenantId, string userId)
        {
            if (String.IsNullOrWhiteSpace(tenantId)) throw new ArgumentException("Tenant ID is required.", nameof(tenantId));
            if (String.IsNullOrWhiteSpace(userId)) throw new ArgumentException("User ID is required.", nameof(userId));

            LedgerAccountLink? accountLink = RequirePlatform().LedgerAccountLinks.FirstOrDefault(item => item.XenoTenantId == tenantId && item.XenoUserId == userId);
            LedgerTenantLink? tenantLink = RequirePlatform().LedgerTenantLinks.FirstOrDefault(item => item.XenoTenantId == tenantId);
            LedgerBalanceResponse response = new LedgerBalanceResponse
            {
                XenoTenantId = tenantId,
                XenoUserId = userId,
                NetLedgerTenantId = tenantLink?.NetLedgerTenantId,
                NetLedgerAccountId = accountLink?.NetLedgerAccountId
            };

            foreach (RequestHistoryEntry entry in LedgerEligibleHistory(tenantId))
            {
                int totalTokens = Math.Max(0, entry.Telemetry?.TotalTokens ?? 0);
                if (totalTokens <= 0) continue;
                string? requestUserId = ResolveRequestUserId(entry);
                if (String.Equals(requestUserId, userId, StringComparison.OrdinalIgnoreCase))
                {
                    response.DebitTotal += totalTokens;
                }

                string? servingUserId = ResolveServingUserId(entry);
                if (String.Equals(servingUserId, userId, StringComparison.OrdinalIgnoreCase))
                {
                    response.CreditTotal += totalTokens;
                }
            }

            response.Balance = response.CreditTotal - response.DebitTotal;
            return response;
        }

        private LedgerEntrySearchResult SearchLedgerEntries(RequestContext requestContext, LedgerEntrySearchRequest request)
        {
            if (request == null) throw new ArgumentNullException(nameof(request));
            string tenantId = String.IsNullOrWhiteSpace(request.TenantId) ? requestContext.TenantId : request.TenantId;
            if (!IsTenantVisible(requestContext, tenantId)) throw new AuthorizationDeniedException(403, "TenantMismatch", "Authenticated tenant cannot access the requested tenant.");
            if (!String.IsNullOrWhiteSpace(request.UserId)) EnsureCanReadLedgerUser(requestContext, tenantId, request.UserId);

            List<LedgerEntryRecord> records = new List<LedgerEntryRecord>();
            foreach (RequestHistoryEntry entry in LedgerEligibleHistory(tenantId))
            {
                if (request.FromUtc.HasValue && (entry.CompletedUtc ?? entry.CreatedUtc) < request.FromUtc.Value.ToUniversalTime()) continue;
                if (request.ToUtc.HasValue && (entry.CompletedUtc ?? entry.CreatedUtc) > request.ToUtc.Value.ToUniversalTime()) continue;
                if (!String.IsNullOrWhiteSpace(request.RequestHistoryId) && entry.Id != request.RequestHistoryId) continue;
                if (!String.IsNullOrWhiteSpace(request.SelectedClientId) && entry.SelectedClientId != request.SelectedClientId) continue;
                if (!String.IsNullOrWhiteSpace(request.RequestCredentialId) && entry.CredentialId != request.RequestCredentialId) continue;

                LedgerEntryRecord? debit = BuildLedgerEntryRecord(entry, LedgerEntryDirection.Debit);
                LedgerEntryRecord? credit = BuildLedgerEntryRecord(entry, LedgerEntryDirection.Credit);
                AddLedgerEntryIfMatched(records, debit, request);
                AddLedgerEntryIfMatched(records, credit, request);
            }

            records = records
                .OrderByDescending(item => item.OccurredUtc ?? item.CreatedUtc ?? DateTime.MinValue)
                .ToList();
            int skip = ParseContinuationToken(request.ContinuationToken);
            int pageSize = Math.Clamp(request.PageSize, 1, 1000);
            List<LedgerEntryRecord> page = records.Skip(skip).Take(pageSize).ToList();
            int next = skip + page.Count;
            return new LedgerEntrySearchResult
            {
                Items = page,
                EndOfResults = next >= records.Count,
                ContinuationToken = next >= records.Count ? null : next.ToString(CultureInfo.InvariantCulture)
            };
        }

        private void AddLedgerEntryIfMatched(List<LedgerEntryRecord> records, LedgerEntryRecord? record, LedgerEntrySearchRequest request)
        {
            if (record == null) return;
            if (request.Direction.HasValue && record.Direction != request.Direction.Value) return;
            if (!String.IsNullOrWhiteSpace(request.UserId) && record.XenoUserId != request.UserId) return;
            records.Add(record);
        }

        private LedgerEntryRecord? BuildLedgerEntryRecord(RequestHistoryEntry entry, LedgerEntryDirection direction)
        {
            int totalTokens = Math.Max(0, entry.Telemetry?.TotalTokens ?? 0);
            if (totalTokens <= 0) return null;
            string? requestUserId = ResolveRequestUserId(entry);
            string? servingUserId = ResolveServingUserId(entry);
            string? userId = direction == LedgerEntryDirection.Debit ? requestUserId : servingUserId;
            if (String.IsNullOrWhiteSpace(userId)) return null;

            LedgerEvent ledgerEvent = new LedgerEvent
            {
                XenoTenantId = entry.TenantId,
                RequestHistoryId = entry.Id,
                RequestCredentialId = entry.CredentialId,
                RequestUserId = requestUserId ?? String.Empty,
                SelectedClientId = entry.SelectedClientId ?? String.Empty,
                ServingUserId = servingUserId ?? String.Empty,
                RequestKind = entry.Path.Contains("embed", StringComparison.OrdinalIgnoreCase) ? LedgerRequestKind.Embedding : LedgerRequestKind.Completion,
                TotalTokens = totalTokens,
                OccurredUtc = (entry.CompletedUtc ?? entry.CreatedUtc).ToUniversalTime()
            };

            return new LedgerEntryRecord
            {
                LedgerEventId = ledgerEvent.Id,
                XenoTenantId = entry.TenantId,
                XenoUserId = userId,
                RequestHistoryId = entry.Id,
                SelectedClientId = entry.SelectedClientId,
                Direction = direction,
                Amount = totalTokens,
                IsCommitted = true,
                Labels = LedgerMetadataBuilder.BuildEntryLabels(direction),
                Tags = LedgerMetadataBuilder.BuildEntryTags(ledgerEvent, direction),
                OccurredUtc = ledgerEvent.OccurredUtc,
                CreatedUtc = entry.CreatedUtc
            };
        }

        private LedgerReconcileResponse BuildLedgerReconcileResponse(RequestContext requestContext)
        {
            LedgerReconcileResponse response = new LedgerReconcileResponse();
            IEnumerable<Tenant> tenants = CanManageTenant(requestContext, requestContext.TenantId)
                ? RequirePlatform().Tenants.Where(item => item.Active)
                : RequirePlatform().Tenants.Where(item => item.Id == requestContext.TenantId && item.Active);

            foreach (Tenant tenant in tenants)
            {
                if (!RequirePlatform().LedgerTenantLinks.Any(item => item.XenoTenantId == tenant.Id))
                {
                    response.MissingTenantLinks.Add(tenant.Id);
                }

                foreach (User user in RequirePlatform().Users.Where(item => item.TenantId == tenant.Id && item.Active))
                {
                    if (!RequirePlatform().LedgerAccountLinks.Any(item => item.XenoTenantId == tenant.Id && item.XenoUserId == user.Id))
                    {
                        response.MissingAccountLinks.Add(tenant.Id + ":" + user.Id);
                    }
                }
            }

            return response;
        }

        private IEnumerable<RequestHistoryEntry> LedgerEligibleHistory(string tenantId)
        {
            return RequirePlatform().RequestHistory.Where(item =>
                item.TenantId == tenantId
                && item.StatusCode >= 200
                && item.StatusCode < 300
                && Math.Max(0, item.Telemetry?.TotalTokens ?? 0) > 0);
        }

        private string ResolveContextUserId(RequestContext requestContext)
        {
            if (!String.IsNullOrWhiteSpace(requestContext.UserId)) return requestContext.UserId;
            if (!String.IsNullOrWhiteSpace(requestContext.CredentialId))
            {
                Credential? credential = RequirePlatform().Credentials.FirstOrDefault(item => item.Id == requestContext.CredentialId && item.TenantId == requestContext.TenantId);
                if (!String.IsNullOrWhiteSpace(credential?.UserId)) return credential.UserId;
            }

            if (requestContext.PrincipalType == PrincipalTypeEnum.User && !String.IsNullOrWhiteSpace(requestContext.PrincipalId)) return requestContext.PrincipalId;
            throw new AuthorizationDeniedException(403, "LedgerUserNotResolved", "Authenticated ledger user could not be resolved.");
        }

        private string? ResolveRequestUserId(RequestHistoryEntry entry)
        {
            if (!String.IsNullOrWhiteSpace(entry.CredentialId))
            {
                Credential? credential = RequirePlatform().Credentials.FirstOrDefault(item => item.Id == entry.CredentialId && item.TenantId == entry.RequestorTenantId);
                if (!String.IsNullOrWhiteSpace(credential?.UserId)) return credential.UserId;
            }

            if (!String.IsNullOrWhiteSpace(entry.UserId)) return entry.UserId;
            if (entry.RequestorPrincipalType == PrincipalTypeEnum.User && !String.IsNullOrWhiteSpace(entry.RequestorPrincipalId)) return entry.RequestorPrincipalId;
            return null;
        }

        private string? ResolveServingUserId(RequestHistoryEntry entry)
        {
            if (String.IsNullOrWhiteSpace(entry.SelectedClientId)) return null;
            XenoClientRecord? client = RequirePlatform().Clients.FirstOrDefault(item => item.Id == entry.SelectedClientId && item.TenantId == entry.TenantId);
            return client?.OwnerUserId;
        }

        private void EnsureCanReadLedgerUser(RequestContext requestContext, string tenantId, string userId)
        {
            if (CanManageTenant(requestContext, tenantId)) return;
            string currentUserId = ResolveContextUserId(requestContext);
            if (String.Equals(currentUserId, userId, StringComparison.OrdinalIgnoreCase)) return;
            throw new AuthorizationDeniedException(403, "LedgerUserDenied", "Authenticated user cannot read another user's ledger.");
        }

        private static int ParseContinuationToken(string? continuationToken)
        {
            if (String.IsNullOrWhiteSpace(continuationToken)) return 0;
            if (!Int32.TryParse(continuationToken, NumberStyles.Integer, CultureInfo.InvariantCulture, out int parsed)) return 0;
            return Math.Max(0, parsed);
        }

        private static int ParsePositiveIntQuery(HttpContextBase context, string name, int defaultValue, int maximum)
        {
            string value = DecodeQueryValue(context.Request.Query.Elements.Get(name));
            if (!Int32.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out int parsed)) return defaultValue;
            return Math.Clamp(parsed, 1, maximum);
        }

        private int DeleteRequestHistory(RequestContext requestContext, RequestHistoryDeleteRequest body)
        {
            HashSet<string> ids = GetStringSet(body.Ids);
            string? singleId = body.Id;
            if (!String.IsNullOrWhiteSpace(singleId)) ids.Add(singleId);
            string? model = body.Model;
            string? path = body.Path;
            string? selectedClientId = body.SelectedClientId;

            return RequirePlatform().RequestHistory.RemoveAll(item =>
                item.TenantId == requestContext.TenantId
                && (ids.Count == 0 || ids.Contains(item.Id))
                && MatchesOptionalFilter(item.Model, model)
                && MatchesOptionalFilter(item.Path, path)
                && MatchesOptionalFilter(item.SelectedClientId, selectedClientId));
        }

        private RequestContext CreateRequestContext(HttpContextBase context)
        {
            string? tenantHint = ResolveTenantHint(context, null);
            string tenantId = tenantHint ?? Constants.DefaultTenantId;

            string authorization = context.Request.Headers.Get("Authorization") ?? String.Empty;
            string tokenHeader = context.Request.Headers.Get("x-token") ?? String.Empty;
            string bearerToken = authorization.StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase) ? authorization.Substring(7).Trim() : String.Empty;
            if (!String.IsNullOrWhiteSpace(bearerToken) && !String.IsNullOrWhiteSpace(tokenHeader) && !String.Equals(bearerToken, tokenHeader, StringComparison.Ordinal))
            {
                throw new UnauthorizedAccessException("Authorization bearer token and x-token do not match.");
            }

            string sessionToken = FirstNonEmpty(bearerToken, tokenHeader);
            string accessKey = context.Request.Headers.Get("x-access-key") ?? String.Empty;
            string secretKey = context.Request.Headers.Get("x-secret-key") ?? String.Empty;
            if (!String.IsNullOrWhiteSpace(secretKey)) throw new UnauthorizedAccessException("x-secret-key is not accepted for authentication.");

            RequestContext requestContext = new RequestContext
            {
                TenantId = tenantId,
                IsAuthenticated = false
            };
            ApplyRequestMetadata(context, requestContext);

            if (!String.IsNullOrWhiteSpace(sessionToken))
            {
                AuthSession? session = _Authentication.ValidateToken(RequirePlatform(), sessionToken);
                if (session != null)
                {
                    if (!TenantHintMatches(tenantHint, session.TenantId)) throw new UnauthorizedAccessException("Authenticated tenant does not match supplied tenant.");
                    requestContext.TenantId = session.TenantId;
                    requestContext.IsAuthenticated = true;
                    requestContext.SessionId = session.Id;
                    requestContext.UserId = session.UserId;
                    requestContext.CredentialId = session.CredentialId;
                    requestContext.PrincipalType = session.PrincipalType;
                    requestContext.AuthScheme = AuthSchemeEnum.Bearer;
                    Interlocked.Increment(ref _AuthSuccessCount);
                    return requestContext;
                }

                Credential? bearerCredential = _Authentication.ValidateCredential(RequirePlatform(), sessionToken);
                if (bearerCredential == null) throw new UnauthorizedAccessException("Invalid or expired token.");
                if (!TenantHintMatches(tenantHint, bearerCredential.TenantId)) throw new UnauthorizedAccessException("Authenticated tenant does not match supplied tenant.");
                requestContext.TenantId = bearerCredential.TenantId;
                requestContext.IsAuthenticated = true;
                requestContext.CredentialId = bearerCredential.Id;
                requestContext.UserId = bearerCredential.UserId;
                requestContext.PrincipalType = PrincipalTypeEnum.Credential;
                requestContext.AuthScheme = AuthSchemeEnum.Bearer;
                Interlocked.Increment(ref _AuthSuccessCount);
                return requestContext;
            }

            if (!String.IsNullOrWhiteSpace(accessKey))
            {
                Credential? credential = _Authentication.ValidateCredential(RequirePlatform(), tenantId, accessKey);
                if (credential == null) throw new UnauthorizedAccessException("Invalid credential.");
                requestContext.IsAuthenticated = true;
                requestContext.CredentialId = credential.Id;
                requestContext.UserId = credential.UserId;
                requestContext.PrincipalType = PrincipalTypeEnum.Credential;
                requestContext.AuthScheme = AuthSchemeEnum.Credential;
                Interlocked.Increment(ref _AuthSuccessCount);
                return requestContext;
            }

            requestContext.IsAuthenticated = false;
            requestContext.UserId = null;
            requestContext.PrincipalType = PrincipalTypeEnum.None;
            requestContext.AuthScheme = AuthSchemeEnum.None;
            return requestContext;
        }

        private RequestContext CreateWebSocketRequestContext(HttpContextBase context)
        {
            string tenantId = ResolveTenantId(context, null);
            string authorization = context.Request.Headers.Get("Authorization") ?? String.Empty;
            string tokenHeader = context.Request.Headers.Get("x-token") ?? String.Empty;
            if (!String.IsNullOrWhiteSpace(authorization) || !String.IsNullOrWhiteSpace(tokenHeader))
            {
                throw new UnauthorizedAccessException("WebSocket clients must authenticate with x-access-key, not bearer tokens or user passwords.");
            }

            string accessKey = context.Request.Headers.Get("x-access-key") ?? String.Empty;
            string secretKey = context.Request.Headers.Get("x-secret-key") ?? String.Empty;
            if (!String.IsNullOrWhiteSpace(secretKey)) throw new UnauthorizedAccessException("x-secret-key is not accepted for WebSocket authentication.");
            RequestContext requestContext = new RequestContext
            {
                TenantId = tenantId,
                IsAuthenticated = false
            };
            ApplyRequestMetadata(context, requestContext);

            if (!String.IsNullOrWhiteSpace(accessKey))
            {
                Credential? credential = _Authentication.ValidateCredential(RequirePlatform(), tenantId, accessKey);
                if (credential == null) throw new UnauthorizedAccessException("Invalid WebSocket credential.");
                requestContext.IsAuthenticated = true;
                requestContext.CredentialId = credential.Id;
                requestContext.UserId = credential.UserId;
                requestContext.PrincipalType = PrincipalTypeEnum.Credential;
                requestContext.AuthScheme = AuthSchemeEnum.Credential;
                return requestContext;
            }

            throw new UnauthorizedAccessException("WebSocket authentication requires a client access key.");
        }

        private static void ApplyRequestMetadata(HttpContextBase context, RequestContext requestContext)
        {
            if (context == null) throw new ArgumentNullException(nameof(context));
            if (requestContext == null) throw new ArgumentNullException(nameof(requestContext));

            requestContext.SourceIp = BoundMetadataValue(context.Request.Source.IpAddress, 128);
            requestContext.ForwardedFor = BoundMetadataValue(FirstNonEmpty(context.Request.Headers.Get("X-Forwarded-For"), context.Request.Headers.Get("x-forwarded-for")), 512);
            requestContext.UserAgent = BoundMetadataValue(FirstNonEmpty(context.Request.Headers.Get("User-Agent"), context.Request.Headers.Get("user-agent")), 512);
            requestContext.Origin = BoundMetadataValue(FirstNonEmpty(context.Request.Headers.Get("Origin"), context.Request.Headers.Get("origin")), 512);
            requestContext.Referer = BoundMetadataValue(FirstNonEmpty(context.Request.Headers.Get("Referer"), context.Request.Headers.Get("Referrer"), context.Request.Headers.Get("referer"), context.Request.Headers.Get("referrer")), 512);
            requestContext.Host = BoundMetadataValue(FirstNonEmpty(context.Request.Headers.Get("Host"), context.Request.Headers.Get("host")), 256);
            requestContext.RequestMethod = BoundMetadataValue(context.Request.Method.ToString().ToUpperInvariant(), 16);
            requestContext.RequestPath = BoundMetadataValue(context.Request.Url.RawWithoutQuery ?? "/", 512);
            requestContext.CorrelationId = BoundMetadataValue(FirstNonEmpty(context.Request.Headers.Get("X-Xeno-Request-ID"), context.Request.Headers.Get("x-xeno-request-id"), context.Request.Headers.Get("X-Request-ID"), context.Request.Headers.Get("x-request-id")), 128);
        }

        private static string BoundMetadataValue(string? value, int maximumLength)
        {
            if (String.IsNullOrWhiteSpace(value)) return String.Empty;
            string normalized = value.Trim();
            if (maximumLength <= 0 || normalized.Length <= maximumLength) return normalized;
            return normalized.Substring(0, maximumLength);
        }

        private static string ResolveTenantId(HttpContextBase context, string? bodyTenantId)
        {
            return ResolveTenantHint(context, bodyTenantId) ?? Constants.DefaultTenantId;
        }

        private static string? ResolveTenantHint(HttpContextBase context, string? bodyTenantId)
        {
            string? tenantHeader = context.Request.Headers.Get("x-tenant-guid");
            string? tenantHeaderAlternate = context.Request.Headers.Get("x-tenant-id");
            string? tenantQuery = context.Request.Query.Elements.Get("tenantId");

            List<string> hints = new List<string>();
            AddNonEmpty(hints, tenantHeader);
            AddNonEmpty(hints, tenantHeaderAlternate);
            AddNonEmpty(hints, tenantQuery);
            AddNonEmpty(hints, bodyTenantId);
            if (hints.Distinct(StringComparer.OrdinalIgnoreCase).Count() > 1)
            {
                throw new ArgumentException("Conflicting tenant identifiers were supplied.");
            }

            string tenantHint = FirstNonEmpty(tenantHeader, tenantHeaderAlternate, tenantQuery, bodyTenantId);
            return String.IsNullOrWhiteSpace(tenantHint) ? null : tenantHint;
        }

        private static bool TenantHintMatches(string? tenantHint, string tenantId)
        {
            return String.IsNullOrWhiteSpace(tenantHint) || String.Equals(tenantHint, tenantId, StringComparison.OrdinalIgnoreCase);
        }

        private static HashSet<string> GetStringSet(List<string>? source)
        {
            HashSet<string> values = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            if (source == null) return values;

            foreach (string item in source)
            {
                if (!String.IsNullOrWhiteSpace(item)) values.Add(item);
            }

            return values;
        }

        private static bool MatchesOptionalFilter(string? value, string? filter)
        {
            if (String.IsNullOrWhiteSpace(filter)) return true;
            if (String.Equals(filter, "unknown", StringComparison.OrdinalIgnoreCase) && String.IsNullOrWhiteSpace(value)) return true;
            return String.Equals(value, filter, StringComparison.OrdinalIgnoreCase);
        }

        private bool IsTenantVisible(RequestContext context, string tenantId)
        {
            return _Authorization.CanAccessTenant(RequirePlatform(), context, tenantId);
        }

        private static bool FixedTimeEquals(string left, string right)
        {
            byte[] leftBytes = Encoding.UTF8.GetBytes(left ?? String.Empty);
            byte[] rightBytes = Encoding.UTF8.GetBytes(right ?? String.Empty);
            if (leftBytes.Length != rightBytes.Length) return false;
            return CryptographicOperations.FixedTimeEquals(leftBytes, rightBytes);
        }

        private static object RedactCredential(Credential credential)
        {
            return new
            {
                credential.Id,
                credential.TenantId,
                credential.UserId,
                credential.AccessKey,
                credential.Active
            };
        }

        private static object RedactUser(User user)
        {
            return new
            {
                user.Id,
                user.TenantId,
                user.Username,
                user.Email,
                user.IsAdmin,
                user.IsTenantAdmin,
                user.Active
            };
        }

        private async Task SendMetricsAsync(HttpContextBase context)
        {
            XenoPlatform platform = RequirePlatform();
            int connectedClients = platform.Clients.Count(item => item.Connected);
            int availableModels = platform.Clients.Where(item => item.Connected).SelectMany(item => item.Models).Select(item => item.Name).Distinct(StringComparer.OrdinalIgnoreCase).Count();
            int requests = platform.RequestHistory.Count;
            int failures = platform.RequestHistory.Count(item => item.StatusCode >= 400);
            context.Response.ContentType = "text/plain";
            string metrics =
                "# HELP xeno_connected_clients Connected Xeno clients\n" +
                "# TYPE xeno_connected_clients gauge\n" +
                "xeno_connected_clients " + connectedClients + "\n" +
                "# HELP xeno_available_models Models advertised by connected Xeno clients\n" +
                "# TYPE xeno_available_models gauge\n" +
                "xeno_available_models " + availableModels + "\n" +
                "# HELP xeno_request_history_total Captured request history entries\n" +
                "# TYPE xeno_request_history_total counter\n" +
                "xeno_request_history_total " + requests + "\n" +
                "# HELP xeno_request_failures_total Captured failed request entries\n" +
                "# TYPE xeno_request_failures_total counter\n" +
                "xeno_request_failures_total " + failures + "\n" +
                "# HELP xeno_auth_success_total Successful authenticated request contexts\n" +
                "# TYPE xeno_auth_success_total counter\n" +
                "xeno_auth_success_total " + Interlocked.Read(ref _AuthSuccessCount).ToString(CultureInfo.InvariantCulture) + "\n" +
                "# HELP xeno_auth_failure_total Authentication failures\n" +
                "# TYPE xeno_auth_failure_total counter\n" +
                "xeno_auth_failure_total " + Interlocked.Read(ref _AuthFailureCount).ToString(CultureInfo.InvariantCulture) + "\n" +
                "# HELP xeno_authorization_denial_total Authorization denials\n" +
                "# TYPE xeno_authorization_denial_total counter\n" +
                "xeno_authorization_denial_total " + Interlocked.Read(ref _AuthorizationDenialCount).ToString(CultureInfo.InvariantCulture) + "\n" +
                "# HELP xeno_privileged_bypass_total System-admin and tenant-admin bypass authorizations\n" +
                "# TYPE xeno_privileged_bypass_total counter\n" +
                "xeno_privileged_bypass_total " + Interlocked.Read(ref _PrivilegedBypassCount).ToString(CultureInfo.InvariantCulture) + "\n" +
                "# HELP xeno_email_sent_total Accepted email messages\n" +
                "# TYPE xeno_email_sent_total counter\n" +
                "xeno_email_sent_total " + Interlocked.Read(ref _EmailSentCount).ToString(CultureInfo.InvariantCulture) + "\n" +
                BuildEmailMetricSeries("xeno_email_sent_by_template_total", "Accepted email messages by provider and template", _EmailSentByProviderTemplate) +
                "# HELP xeno_email_skipped_total Skipped email messages\n" +
                "# TYPE xeno_email_skipped_total counter\n" +
                "xeno_email_skipped_total " + Interlocked.Read(ref _EmailSkippedCount).ToString(CultureInfo.InvariantCulture) + "\n" +
                BuildEmailMetricSeries("xeno_email_skipped_by_template_total", "Skipped email messages by provider and template", _EmailSkippedByProviderTemplate) +
                "# HELP xeno_email_failed_total Failed email messages\n" +
                "# TYPE xeno_email_failed_total counter\n" +
                "xeno_email_failed_total " + Interlocked.Read(ref _EmailFailureCount).ToString(CultureInfo.InvariantCulture) + "\n" +
                BuildEmailMetricSeries("xeno_email_failed_by_template_total", "Failed email messages by provider and template", _EmailFailedByProviderTemplate) +
                "# HELP xeno_email_last_failure_unix_seconds Last email failure timestamp\n" +
                "# TYPE xeno_email_last_failure_unix_seconds gauge\n" +
                "xeno_email_last_failure_unix_seconds " + EmailLastFailureUnixSeconds().ToString(CultureInfo.InvariantCulture) + "\n";
            await context.Response.Send(metrics, context.Token).ConfigureAwait(false);
        }

        private void RecordEmailResult(EmailSendResult result)
        {
            if (result == null) return;
            if (result.Skipped)
            {
                Interlocked.Increment(ref _EmailSkippedCount);
                IncrementEmailSeries(_EmailSkippedByProviderTemplate, result.Provider, result.TemplateId);
                return;
            }

            if (result.Accepted)
            {
                Interlocked.Increment(ref _EmailSentCount);
                IncrementEmailSeries(_EmailSentByProviderTemplate, result.Provider, result.TemplateId);
            }
            else
            {
                RecordEmailFailure(result.Provider, result.TemplateId);
            }
        }

        private void RecordEmailFailure(string provider, string templateId)
        {
            Interlocked.Increment(ref _EmailFailureCount);
            Interlocked.Exchange(ref _EmailLastFailureTicks, DateTime.UtcNow.Ticks);
            IncrementEmailSeries(_EmailFailedByProviderTemplate, provider, templateId);
        }

        private static void IncrementEmailSeries(ConcurrentDictionary<string, long> series, string provider, string templateId)
        {
            string key = FirstNonEmpty(provider, "unknown") + "|" + FirstNonEmpty(templateId, "unknown");
            series.AddOrUpdate(key, 1, delegate(string existingKey, long current)
            {
                return current + 1;
            });
        }

        private string BuildEmailMetricSeries(string metricName, string help, ConcurrentDictionary<string, long> series)
        {
            StringBuilder builder = new StringBuilder();
            builder.Append("# HELP ");
            builder.Append(metricName);
            builder.Append(' ');
            builder.Append(help);
            builder.Append('\n');
            builder.Append("# TYPE ");
            builder.Append(metricName);
            builder.Append(" counter\n");
            foreach (KeyValuePair<string, long> item in series.OrderBy(entry => entry.Key, StringComparer.OrdinalIgnoreCase))
            {
                string[] parts = item.Key.Split('|');
                string provider = parts.Length > 0 ? parts[0] : "unknown";
                string template = parts.Length > 1 ? parts[1] : "unknown";
                builder.Append(metricName);
                builder.Append("{provider=\"");
                builder.Append(EscapePrometheusLabel(provider));
                builder.Append("\",template=\"");
                builder.Append(EscapePrometheusLabel(template));
                builder.Append("\"} ");
                builder.Append(item.Value.ToString(CultureInfo.InvariantCulture));
                builder.Append('\n');
            }

            return builder.ToString();
        }

        private double EmailLastFailureUnixSeconds()
        {
            long ticks = Interlocked.Read(ref _EmailLastFailureTicks);
            if (ticks <= 0) return 0;
            return new DateTimeOffset(new DateTime(ticks, DateTimeKind.Utc)).ToUnixTimeSeconds();
        }

        private static string EscapePrometheusLabel(string value)
        {
            return (value ?? String.Empty)
                .Replace("\\", "\\\\", StringComparison.Ordinal)
                .Replace("\"", "\\\"", StringComparison.Ordinal)
                .Replace("\n", "\\n", StringComparison.Ordinal);
        }

        private object AnalyticsOverview(RequestContext requestContext, HttpContextBase context)
        {
            DateTime? fromUtc = ParseDateQuery(context, "fromUtc");
            DateTime? toUtc = ParseDateQuery(context, "toUtc");
            List<RequestHistoryEntry> entries = FilterRequestHistory(requestContext, fromUtc, toUtc);
            int total = entries.Count;
            int failures = entries.Count(item => item.StatusCode >= 400);
            List<XenoClientRecord> clients = RequirePlatform().Clients
                .Where(item => item.TenantId == requestContext.TenantId)
                .ToList();
            List<XenoClientModel> clientModels = clients
                .SelectMany(item => item.Models)
                .ToList();
            List<object> byModel = entries
                .Where(item => item.RoutingDecision != null && !String.IsNullOrWhiteSpace(item.RoutingDecision.EffectiveModel))
                .GroupBy(item => item.RoutingDecision!.EffectiveModel)
                .Select(group => (object)new
                {
                    name = group.Key,
                    model = group.Key,
                    count = group.Count(),
                    success = group.Count(item => item.StatusCode < 400),
                    failed = group.Count(item => item.StatusCode >= 400)
                })
                .ToList();
            List<object> byPath = entries
                .Where(item => !String.IsNullOrWhiteSpace(item.Path))
                .GroupBy(item => item.Path)
                .Select(group => (object)new
                {
                    name = group.Key,
                    path = group.Key,
                    count = group.Count(),
                    success = group.Count(item => item.StatusCode < 400),
                    failed = group.Count(item => item.StatusCode >= 400)
                })
                .ToList();
            List<object> byClient = entries
                .Where(item => !String.IsNullOrWhiteSpace(item.SelectedClientId))
                .GroupBy(item => item.SelectedClientId)
                .Select(group => (object)new
                {
                    name = group.Key,
                    clientId = group.Key,
                    count = group.Count(),
                    success = group.Count(item => item.StatusCode < 400),
                    failed = group.Count(item => item.StatusCode >= 400)
                })
                .ToList();

            return new
            {
                total,
                failures,
                success = total - failures,
                failureRate = total == 0 ? 0 : (double)failures / total,
                connectedClientCount = clients.Count(item => item.Connected),
                totalClientCount = clients.Count,
                advertisedModelCount = clientModels
                    .Select(item => item.Name)
                    .Where(item => !String.IsNullOrWhiteSpace(item))
                    .Distinct(StringComparer.OrdinalIgnoreCase)
                    .Count(),
                completionModelCount = clientModels.Count(item => item.SupportsCompletions),
                embeddingModelCount = clientModels.Count(item => item.SupportsEmbeddings),
                byModel,
                byPath,
                byClient
            };
        }

        private RequestHistorySearchResult ListRequestHistory(RequestContext requestContext, HttpContextBase context)
        {
            DateTime? fromUtc = ParseDateQuery(context, "fromUtc");
            DateTime? toUtc = ParseDateQuery(context, "toUtc");
            int page = ParseIntegerQuery(context, "page", 1, 1, 100000);
            int pageSize = ParseIntegerQuery(context, "pageSize", 100, 1, 1000);
            List<RequestHistoryEntry> entries = FilterRequestHistory(requestContext, fromUtc, toUtc);
            int skip = Math.Min(Int32.MaxValue, (page - 1) * pageSize);
            return new RequestHistorySearchResult
            {
                Items = entries.Skip(skip).Take(pageSize).ToList(),
                Total = entries.Count,
                Page = page,
                PageSize = pageSize
            };
        }

        private static ModelTokenLeaderboardRequest BuildModelTokenLeaderboardRequest(HttpContextBase context)
        {
            ModelTokenLeaderboardRequest request = new ModelTokenLeaderboardRequest
            {
                TenantId = DecodeQueryValue(context.Request.Query.Elements.Get("tenantId")),
                FromUtc = ParseDateQuery(context, "fromUtc"),
                ToUtc = ParseDateQuery(context, "toUtc"),
                Limit = ParseIntegerQuery(context, "limit", 100, 1, 1000),
                Offset = ParseIntegerQuery(context, "offset", 0, 0, Int32.MaxValue)
            };

            return NormalizeModelTokenLeaderboardRequest(request);
        }

        private static ModelTokenLeaderboardRequest NormalizeModelTokenLeaderboardRequest(ModelTokenLeaderboardRequest request)
        {
            ArgumentNullException.ThrowIfNull(request);
            request.TenantId = String.IsNullOrWhiteSpace(request.TenantId) ? null : request.TenantId;
            request.Limit = request.Limit.HasValue ? Math.Clamp(request.Limit.Value, 1, 1000) : 100;
            request.Offset = request.Offset.HasValue ? Math.Max(0, request.Offset.Value) : 0;
            if (request.FromUtc.HasValue) request.FromUtc = request.FromUtc.Value.ToUniversalTime();
            if (request.ToUtc.HasValue) request.ToUtc = request.ToUtc.Value.ToUniversalTime();
            return request;
        }

        private List<RequestHistoryEntry> FilterRequestHistory(RequestContext context, DateTime? fromUtc = null, DateTime? toUtc = null)
        {
            IEnumerable<RequestHistoryEntry> entries = RequirePlatform().RequestHistory
                .Where(item => item.TenantId == context.TenantId);
            if (fromUtc.HasValue) entries = entries.Where(item => item.CreatedUtc >= fromUtc.Value);
            if (toUtc.HasValue) entries = entries.Where(item => item.CreatedUtc < toUtc.Value);
            return entries.OrderByDescending(item => item.CreatedUtc).ToList();
        }

        private object SummarizeRequestHistory(RequestContext requestContext, HttpContextBase context)
        {
            DateTime toUtc = DateTime.UtcNow;
            DateTime fromUtc = toUtc.AddHours(-24);
            int bucketMinutes = 15;

            string fromValue = DecodeQueryValue(context.Request.Query.Elements.Get("fromUtc"));
            string toValue = DecodeQueryValue(context.Request.Query.Elements.Get("toUtc"));
            string bucketValue = DecodeQueryValue(context.Request.Query.Elements.Get("bucketMinutes"));

            if (DateTime.TryParse(fromValue, out DateTime parsedFrom)) fromUtc = parsedFrom.ToUniversalTime();
            if (DateTime.TryParse(toValue, out DateTime parsedTo)) toUtc = parsedTo.ToUniversalTime();
            bucketMinutes = DefaultRequestHistoryBucketMinutes(fromUtc, toUtc);
            if (Int32.TryParse(bucketValue, out int parsedBucket) && parsedBucket > 0) bucketMinutes = parsedBucket;

            List<RequestHistoryEntry> entries = FilterRequestHistory(requestContext, fromUtc, toUtc);
            List<object> buckets = new List<object>();
            DateTime cursor = fromUtc;
            while (cursor < toUtc)
            {
                DateTime bucketEnd = cursor.AddMinutes(bucketMinutes);
                int count = entries.Count(item => item.CreatedUtc >= cursor && item.CreatedUtc < bucketEnd);
                int errors = entries.Count(item => item.CreatedUtc >= cursor && item.CreatedUtc < bucketEnd && item.StatusCode >= 400);
                buckets.Add(new { startUtc = cursor, endUtc = bucketEnd, count, errors, success = count - errors, failed = errors });
                cursor = bucketEnd;
            }

            return new { fromUtc, toUtc, bucketMinutes, buckets };
        }

        private static DateTime? ParseDateQuery(HttpContextBase context, string name)
        {
            string value = DecodeQueryValue(context.Request.Query.Elements.Get(name));
            if (DateTime.TryParse(value, out DateTime parsed)) return parsed.ToUniversalTime();
            return null;
        }

        private static int ParseIntegerQuery(HttpContextBase context, string name, int defaultValue, int minimum, int maximum)
        {
            string value = DecodeQueryValue(context.Request.Query.Elements.Get(name));
            if (!Int32.TryParse(value, out int parsed)) return defaultValue;
            return Math.Min(maximum, Math.Max(minimum, parsed));
        }

        private static int DefaultRequestHistoryBucketMinutes(DateTime fromUtc, DateTime toUtc)
        {
            double minutes = Math.Max(1, (toUtc - fromUtc).TotalMinutes);
            if (minutes <= 60) return 1;
            if (minutes <= 24 * 60) return 15;
            if (minutes <= 7 * 24 * 60) return 120;
            return 480;
        }

        private static string ReadBodyJson(HttpContextBase context)
        {
            string json = context.Request.DataAsString ?? String.Empty;
            return String.IsNullOrWhiteSpace(json) ? "{}" : json;
        }

        private static T ReadJsonBody<T>(HttpContextBase context) where T : class, new()
        {
            string json = ReadBodyJson(context);
            T? body = JsonUtil.Deserialize<T>(json);
            return body ?? new T();
        }

        private async Task SendJsonAsync(HttpContextBase context, object value)
        {
            context.Response.ContentType = "application/json";
            await context.Response.Send(JsonUtil.Serialize(value, true), context.Token).ConfigureAwait(false);
        }

        private async Task SendStatusJsonAsync(HttpContextBase context, int statusCode, object value)
        {
            context.Response.StatusCode = statusCode;
            await SendJsonAsync(context, value).ConfigureAwait(false);
        }

        private async Task SendNoContentAsync(HttpContextBase context)
        {
            context.Response.StatusCode = 204;
            await context.Response.Send(context.Token).ConfigureAwait(false);
        }

        private void ApplyCors(HttpContextBase context)
        {
            if (!_Settings.Cors.Enabled) return;
            HttpResponseBase response = context.Response;
            string requestOrigin = FirstNonEmpty(context.Request.Headers.Get("Origin"), context.Request.Headers.Get("origin"));
            string allowedOrigin = ResolveCorsOrigin(requestOrigin);
            if (!String.IsNullOrWhiteSpace(allowedOrigin)) response.Headers.Add("Access-Control-Allow-Origin", allowedOrigin);
            response.Headers.Add("Vary", "Origin");
            response.Headers.Add("Access-Control-Allow-Headers", String.Join(",", _Settings.Cors.AllowedHeaders));
            response.Headers.Add("Access-Control-Allow-Methods", String.Join(",", _Settings.Cors.AllowedMethods));
            response.Headers.Add("Access-Control-Expose-Headers", "Content-Type, Authorization, x-token, x-tenant-guid, " + RoutingTraceExposeHeaders);
        }

        private string ResolveCorsOrigin(string requestOrigin)
        {
            List<string> allowedOrigins = _Settings.Cors.AllowedOrigins ?? new List<string>();
            if (allowedOrigins.Any(item => String.Equals(item, "*", StringComparison.Ordinal))) return "*";
            if (!String.IsNullOrWhiteSpace(requestOrigin) && allowedOrigins.Any(item => String.Equals(item, requestOrigin, StringComparison.OrdinalIgnoreCase)))
            {
                return requestOrigin;
            }

            return allowedOrigins.Count == 1 ? allowedOrigins[0] : String.Empty;
        }

        private XenoPlatform RequirePlatform()
        {
            if (_Database == null) throw new InvalidOperationException("Database has not been initialized.");
            return _Database.Platform;
        }

        private RoutingService RequireRouting()
        {
            if (_Routing == null) throw new InvalidOperationException("Routing has not been initialized.");
            return _Routing;
        }

        private TenantLifecycleService RequireTenantLifecycle()
        {
            if (_TenantLifecycle == null) throw new InvalidOperationException("Tenant lifecycle has not been initialized.");
            return _TenantLifecycle;
        }

        private List<User> ListSeedUsers(string tenantId)
        {
            return RequirePlatform().Users
                .Where(item => String.Equals(item.TenantId, tenantId, StringComparison.OrdinalIgnoreCase))
                .ToList();
        }

        private async Task ProvisionLedgerAccountForUserAsync(User user, RequestContext requestContext, CancellationToken token)
        {
            if (user == null) throw new ArgumentNullException(nameof(user));
            Tenant? tenant = RequirePlatform().Tenants.FirstOrDefault(item => String.Equals(item.Id, user.TenantId, StringComparison.OrdinalIgnoreCase));
            if (tenant == null) return;
            await RequireTenantLifecycle().CreateAsync(tenant, new List<User> { user }, true, requestContext, token).ConfigureAwait(false);
        }

        private static bool IsPath(string path, params string[] candidates)
        {
            return candidates.Any(candidate => path.Equals(candidate, StringComparison.OrdinalIgnoreCase));
        }

        private static string? TryGetPathId(string path, params string[] prefixes)
        {
            foreach (string prefix in prefixes)
            {
                if (!path.StartsWith(prefix, StringComparison.OrdinalIgnoreCase)) continue;
                string id = path.Substring(prefix.Length);
                if (String.IsNullOrWhiteSpace(id)) return null;
                return Uri.UnescapeDataString(id);
            }

            return null;
        }

        private static string DecodeQueryValue(string? value)
        {
            if (String.IsNullOrWhiteSpace(value)) return String.Empty;
            return WebUtility.UrlDecode(value) ?? value;
        }

        private static string FirstNonEmpty(params string?[] values)
        {
            foreach (string? value in values)
            {
                if (!String.IsNullOrWhiteSpace(value)) return value;
            }

            return String.Empty;
        }

        private static void ApplyEnvironmentSettings(XenoSettings settings)
        {
            string ledgerApiKey = FirstNonEmpty(Environment.GetEnvironmentVariable("XENO_LEDGER_API_KEY"), Environment.GetEnvironmentVariable("NETLEDGER_ACCESS_KEY"), Environment.GetEnvironmentVariable("NETLEDGER_ADMIN_KEY"));
            if (!String.IsNullOrWhiteSpace(ledgerApiKey)) settings.Ledger.ApiKey = ledgerApiKey;
            string ledgerSecretKey = FirstNonEmpty(Environment.GetEnvironmentVariable("XENO_LEDGER_SECRET_KEY"), Environment.GetEnvironmentVariable("NETLEDGER_SECRET_KEY"));
            if (!String.IsNullOrWhiteSpace(ledgerSecretKey)) settings.Ledger.SecretKey = ledgerSecretKey;
        }

        private static bool IsValidEmailAddress(string email)
        {
            if (String.IsNullOrWhiteSpace(email)) return false;
            try
            {
                MailAddress address = new MailAddress(email);
                return String.Equals(address.Address, email, StringComparison.OrdinalIgnoreCase);
            }
            catch (FormatException)
            {
                return false;
            }
        }

        private static void AddNonEmpty(List<string> values, string? value)
        {
            if (!String.IsNullOrWhiteSpace(value)) values.Add(value);
        }

        private object BuildOpenApi()
        {
            return new
            {
                openapi = "3.0.1",
                info = new { title = "Xeno API", version = Constants.Version, description = "Xeno AI control plane API." },
                paths = new Dictionary<string, object>
                {
                    ["/"] = Path(Operation("get", "rootHealth", "Health", "Health check", "Returns current server health.", "HealthResponse", false)),
                    ["/health"] = Path(Operation("get", "health", "Health", "Health check", "Returns current server health.", "HealthResponse", false)),
                    ["/v1.0/api/health"] = Path(Operation("get", "apiHealth", "Health", "Health check", "Returns current server health.", "HealthResponse", false)),
                    ["/v1.0/api/ping"] = Path(Operation("get", "ping", "Health", "Ping heartbeat", "Returns a timestamped server heartbeat and echoes the optional sequence and client timestamp query values.", "PingResponse", false)),
                    ["/metrics"] = Path(Operation("get", "metrics", "Health", "Prometheus metrics", "Returns Prometheus-style metrics.", "GenericObject", false)),
                    ["/openapi.json"] = Path(Operation("get", "openApi", "Health", "OpenAPI document", "Returns the OpenAPI document.", "GenericObject", false)),
                    ["/v1.0/api/auth/tenants"] = Path(Operation("get", "lookupLoginTenants", "Authentication", "Lookup login tenants", "Returns active tenants for a login email query string.", "GenericObject", false), Operation("post", "lookupLoginTenantsByBody", "Authentication", "Lookup login tenants", "Returns active tenants for a login email request body.", "GenericObject", false)),
                    ["/v1.0/api/auth/login"] = Path(Operation("post", "login", "Authentication", "Login", "Validates a user password and returns a one-time-visible client credential for WebSocket use.", "CredentialLoginResponse", false)),
                    ["/v1.0/api/onboarding/signup"] = Path(Operation("post", "signup", "Onboarding", "Sign up", "Creates a tenant and first tenant administrator when tenantId is omitted, or adds a regular user to an existing tenant when tenantId is supplied.", "AuthOnboardingResponse", false)),
                    ["/v1.0/api/auth/token"] = Path(Operation("post", "createToken", "Authentication", "Create token", "Creates a development session token.", "TokenResponse", false)),
                    ["/v1.0/api/auth/token/details"] = Path(Operation("get", "tokenDetails", "Authentication", "Token details", "Returns details for the current token.", "GenericObject", true)),
                    ["/v1.0/api/auth/token/refresh"] = Path(Operation("post", "refreshToken", "Authentication", "Refresh token", "Refreshes the current bearer token.", "TokenResponse", true)),
                    ["/v1.0/api/auth/logout"] = Path(Operation("post", "logout", "Authentication", "Logout", "Revokes the current session token.", "GenericObject", true)),
                    ["/v1.0/api/admin/nuke/tenant"] = Path(Operation("post", "nukeTenant", "Administration", "Nuke tenant", "Dry-runs or executes permanent deletion of a tenant and all tenant-owned records. Requires global/system administrator.", "AdminNukeResponse", true)),
                    ["/v1.0/api/admin/nuke/user"] = Path(Operation("post", "nukeUser", "Administration", "Nuke user", "Dry-runs or executes permanent deletion of a user and user-owned records. Requires global/system administrator or same-tenant tenant administrator for non-self targets.", "AdminNukeResponse", true)),
                    ["/v1.0/api/tenants"] = Path(Operation("get", "listTenants", "Tenants", "List tenants", "Lists tenants visible to the caller.", "TenantArray", true), Operation("post", "createTenant", "Tenants", "Create tenant", "Creates a tenant.", "Tenant", true)),
                    ["/v1.0/api/tenants/{id}"] = Path(Operation("put", "updateTenant", "Tenants", "Update tenant", "Updates tenant name and active state.", "Tenant", true), Operation("delete", "deleteTenant", "Tenants", "Delete tenant", "Deletes a tenant and its scoped records.", "GenericObject", true)),
                    ["/v1.0/api/users"] = Path(Operation("get", "listUsers", "Tenants", "List users", "Lists tenant users.", "UserArray", true), Operation("post", "createUser", "Tenants", "Create user", "Creates a tenant user with a required password.", "User", true)),
                    ["/v1.0/api/users/{id}"] = Path(Operation("put", "updateUser", "Tenants", "Update user", "Updates tenant user profile, admin flags, active state, and optional password.", "User", true), Operation("delete", "deleteUser", "Tenants", "Delete user", "Deletes a tenant user and related sessions or credentials.", "GenericObject", true)),
                    ["/v1.0/api/credentials"] = Path(Operation("get", "listCredentials", "Tenants", "List credentials", "Lists tenant credentials.", "CredentialArray", true), Operation("post", "createCredential", "Tenants", "Create credential", "Creates a tenant credential.", "Credential", true)),
                    ["/v1.0/api/credentials/{id}"] = Path(Operation("put", "updateCredential", "Tenants", "Update credential", "Updates credential user, access key, and active state.", "Credential", true), Operation("delete", "deleteCredential", "Tenants", "Delete credential", "Deletes a credential and related sessions.", "GenericObject", true)),
                    ["/v1.0/api/roles"] = Path(Operation("get", "listRoles", "Authorization", "List roles", "Lists tenant roles.", "RoleArray", true), Operation("post", "createRole", "Authorization", "Create role", "Creates a tenant role.", "Role", true)),
                    ["/v1.0/api/roles/{id}"] = Path(Operation("put", "updateRole", "Authorization", "Update role", "Updates role name, permission assignments, and active state.", "Role", true), Operation("delete", "deleteRole", "Authorization", "Delete role", "Deletes a tenant role and related assignments.", "GenericObject", true)),
                    ["/v1.0/api/permissions"] = Path(Operation("get", "listPermissions", "Authorization", "List permissions", "Lists tenant permissions.", "PermissionArray", true), Operation("post", "createPermission", "Authorization", "Create permission", "Creates a tenant permission.", "Permission", true)),
                    ["/v1.0/api/permissions/grantable"] = Path(Operation("get", "listGrantablePermissions", "Authorization", "List grantable permissions", "Lists permissions the caller may assign to roles or principals.", "PermissionArray", true)),
                    ["/v1.0/api/permissions/{id}"] = Path(Operation("put", "updatePermission", "Authorization", "Update permission", "Updates permission resource, operation, permit decision, and active state.", "Permission", true), Operation("delete", "deletePermission", "Authorization", "Delete permission", "Deletes a tenant permission and removes it from roles.", "GenericObject", true)),
                    ["/v1.0/api/role-assignments"] = Path(Operation("get", "listRoleAssignments", "Authorization", "List role assignments", "Lists tenant role assignments.", "RoleAssignmentArray", true), Operation("post", "createRoleAssignment", "Authorization", "Create role assignment", "Assigns a role to a user or credential within grantor authority.", "RoleAssignment", true)),
                    ["/v1.0/api/role-assignments/{id}"] = Path(Operation("delete", "deleteRoleAssignment", "Authorization", "Delete role assignment", "Deletes one tenant role assignment.", "GenericObject", true)),
                    ["/v1.0/api/effective-permissions"] = Path(Operation("get", "listEffectivePermissions", "Authorization", "Effective permissions", "Lists effective permissions.", "EffectivePermissionArray", true)),
                    ["/v1.0/api/audit"] = Path(Operation("get", "listAudit", "Audit", "List audit records", "Lists tenant audit records.", "AuditRecordArray", true)),
                    ["/v1.0/api/audit/{id}"] = Path(Operation("delete", "deleteAuditRecord", "Audit", "Delete audit record", "Deletes one tenant audit record.", "GenericObject", true)),
                    ["/v1.0/api/clients"] = Path(Operation("get", "listClients", "Clients", "List clients", "Lists connected or registered Xeno clients and their advertised models.", "ClientArray", true)),
                    ["/v1.0/api/clients/{id}"] = Path(Operation("delete", "deleteClient", "Clients", "Delete client", "Deletes a registered Xeno client.", "GenericObject", true)),
                    ["/v1.0/api/client-models"] = Path(Operation("delete", "deleteClientModel", "Clients", "Delete client model", "Deletes one model advertised by a client.", "GenericObject", true)),
                    ["/v1.0/api/client-telemetry/status"] = Path(Operation("get", "getClientTelemetryStatus", "Client Telemetry", "Client telemetry status", "Returns collection settings, running state, next cycle, connected clients, and recent failures.", "ServerClientTelemetryCollectionStatusResponse", true)),
                    ["/v1.0/api/client-telemetry/latest"] = Path(Operation("get", "getLatestClientTelemetry", "Client Telemetry", "Latest client telemetry", "Returns latest host and provider telemetry for the tenant.", "ServerClientTelemetryLatestResponse", true)),
                    ["/v1.0/api/client-telemetry/collect"] = Path(Operation("post", "collectClientTelemetry", "Client Telemetry", "Collect client telemetry", "Triggers immediate host and provider telemetry collection.", "ServerClientTelemetryCollectResponse", true)),
                    ["/v1.0/api/client-telemetry/history/search"] = Path(Operation("post", "searchClientTelemetry", "Client Telemetry", "Search host telemetry", "Searches server-collected host telemetry samples.", "ServerClientTelemetrySearchResult", true)),
                    ["/v1.0/api/client-telemetry/history/rollups"] = Path(Operation("post", "rollupClientTelemetry", "Client Telemetry", "Host telemetry rollups", "Returns host telemetry rollup buckets.", "ServerClientTelemetryRollupBucketArray", true)),
                    ["/v1.0/api/client-telemetry/history/{id}"] = Path(Operation("get", "getClientTelemetryDetail", "Client Telemetry", "Get host telemetry detail", "Returns one host telemetry detail record.", "ServerClientTelemetrySampleDetail", true), Operation("delete", "deleteClientTelemetryDetail", "Client Telemetry", "Delete host telemetry detail", "Deletes one host telemetry sample.", "GenericObject", true)),
                    ["/v1.0/api/client-telemetry/history/delete"] = Path(Operation("post", "bulkDeleteClientTelemetry", "Client Telemetry", "Bulk delete host telemetry", "Deletes host telemetry samples by filter.", "GenericObject", true)),
                    ["/v1.0/api/client-telemetry/providers/latest"] = Path(Operation("get", "getLatestClientProviderTelemetry", "Client Telemetry", "Latest provider telemetry", "Returns latest provider telemetry for the tenant.", "ServerProviderTelemetrySampleRecordArray", true)),
                    ["/v1.0/api/client-telemetry/providers/collect"] = Path(Operation("post", "collectClientProviderTelemetry", "Client Telemetry", "Collect provider telemetry", "Triggers immediate provider telemetry collection.", "ServerClientTelemetryCollectResponse", true)),
                    ["/v1.0/api/client-telemetry/providers/history/search"] = Path(Operation("post", "searchClientProviderTelemetry", "Client Telemetry", "Search provider telemetry", "Searches server-collected provider telemetry samples.", "ServerProviderTelemetrySearchResult", true)),
                    ["/v1.0/api/client-telemetry/providers/history/rollups"] = Path(Operation("post", "rollupClientProviderTelemetry", "Client Telemetry", "Provider telemetry rollups", "Returns provider telemetry rollup buckets.", "ServerProviderTelemetryRollupBucketArray", true)),
                    ["/v1.0/api/client-telemetry/providers/history/{id}"] = Path(Operation("get", "getClientProviderTelemetryDetail", "Client Telemetry", "Get provider telemetry detail", "Returns one provider telemetry detail record.", "ServerProviderTelemetrySampleDetail", true), Operation("delete", "deleteClientProviderTelemetryDetail", "Client Telemetry", "Delete provider telemetry detail", "Deletes one provider telemetry sample.", "GenericObject", true)),
                    ["/v1.0/api/client-telemetry/providers/history/delete"] = Path(Operation("post", "bulkDeleteClientProviderTelemetry", "Client Telemetry", "Bulk delete provider telemetry", "Deletes provider telemetry samples by filter.", "GenericObject", true)),
                    ["/v1.0/api/client-telemetry/collection-attempts"] = Path(Operation("get", "listClientTelemetryCollectionAttempts", "Client Telemetry", "List collection attempts", "Lists recent telemetry collection attempts.", "ServerClientTelemetryCollectionAttemptRecordArray", true)),
                    ["/v1.0/api/client-telemetry/metrics"] = Path(Operation("get", "listClientTelemetryMetrics", "Client Telemetry", "Telemetry metric catalog", "Returns the telemetry metric catalog used by dashboard selectors and routing policies.", "RoutingPolicyMetricDefinitionArray", true)),
                    ["/v1.0/api/model-definitions"] = Path(Operation("get", "listModelDefinitions", "Routing", "List model definitions", "Lists model definitions available for routing.", "ModelDefinitionArray", true)),
                    ["/v1.0/api/model-configurations"] = Path(Operation("get", "listModelConfigurations", "Routing", "List model configurations", "Lists pinned model configurations.", "ModelConfigurationArray", true)),
                    ["/v1.0/api/routing-policies"] = Path(Operation("get", "listRoutingPolicies", "Routing", "List routing policies", "Lists tenant-scoped routing policies.", "RoutingPolicyArray", true), Operation("post", "createRoutingPolicy", "Routing", "Create routing policy", "Creates a tenant-scoped routing policy.", "RoutingPolicy", true)),
                    ["/v1.0/api/routing-policies/validate"] = Path(Operation("post", "validateRoutingPolicy", "Routing", "Validate routing policy", "Validates telemetry filters, ranking rules, and freshness bounds without saving a policy.", "RoutingPolicyValidationResult", true)),
                    ["/v1.0/api/routing-policies/{id}"] = Path(Operation("put", "updateRoutingPolicy", "Routing", "Update routing policy", "Updates a tenant-scoped routing policy.", "RoutingPolicy", true), Operation("delete", "deleteRoutingPolicy", "Routing", "Delete routing policy", "Deletes a non-default routing policy.", "GenericObject", true)),
                    ["/v1.0/api/request-history"] = Path(Operation("get", "listRequestHistory", "Request History", "List request history", "Lists captured request history entries.", "RequestHistorySearchResult", true)),
                    ["/v1.0/api/request-history/model-leaderboard"] = Path(Operation("get", "getModelTokenLeaderboard", "Model Leaderboard", "Model token leaderboard", "Returns public ranked model token usage from request history.", "ModelTokenLeaderboardResponse", false), Operation("post", "queryModelTokenLeaderboard", "Model Leaderboard", "Model token leaderboard", "Returns public ranked model token usage from request history.", "ModelTokenLeaderboardResponse", false)),
                    ["/v1.0/api/request-history/{id}"] = Path(Operation("get", "getRequestHistory", "Request History", "Get request history", "Returns a captured request history entry.", "RequestHistoryEntry", true), Operation("delete", "deleteRequestHistory", "Request History", "Delete request history", "Deletes one captured request history entry.", "GenericObject", true)),
                    ["/v1.0/api/request-history/summary"] = Path(Operation("get", "summarizeRequestHistory", "Request History", "Request history summary", "Returns bucketed request counts.", "RequestHistorySummary", true)),
                    ["/v1.0/api/request-history/bulk"] = Path(Operation("post", "bulkDeleteRequestHistory", "Request History", "Bulk delete request history", "Deletes tenant request history entries.", "GenericObject", true)),
                    ["/v1.0/api/request-history/analytics/overview"] = Path(Operation("get", "requestAnalyticsOverview", "Request History", "Analytics overview", "Returns tenant request analytics.", "GenericObject", true)),
                    ["/v1.0/api/analytics/overview"] = Path(Operation("get", "analyticsOverview", "Analytics", "Analytics overview", "Returns tenant analytics.", "GenericObject", true)),
                    ["/v1.0/api/ledger/status"] = Path(Operation("get", "getLedgerStatus", "Ledger", "Ledger status", "Returns in-memory ledger queue status and NetLedger health.", "LedgerStatusResponse", true)),
                    ["/v1.0/api/ledger/balance/me"] = Path(Operation("get", "getMyLedgerBalance", "Ledger", "My ledger balance", "Returns the authenticated user's token ledger balance derived from accounted request history.", "LedgerBalanceResponse", true)),
                    ["/v1.0/api/ledger/balances"] = Path(Operation("get", "searchLedgerBalances", "Ledger", "Ledger balances", "Lists ledger balances visible to the caller.", "LedgerBalanceSearchResult", true)),
                    ["/v1.0/api/ledger/balances/users/{userId}"] = Path(Operation("get", "getLedgerUserBalance", "Ledger", "User ledger balance", "Returns one user's ledger balance when the caller is authorized for that user.", "LedgerBalanceResponse", true)),
                    ["/v1.0/api/ledger/entries/me"] = Path(Operation("get", "getMyLedgerEntries", "Ledger", "My ledger entries", "Returns the authenticated user's ledger activity.", "LedgerEntrySearchResult", true)),
                    ["/v1.0/api/ledger/entries/search"] = Path(Operation("post", "searchLedgerEntries", "Ledger", "Search ledger entries", "Searches ledger activity visible to the caller.", "LedgerEntrySearchResult", true)),
                    ["/v1.0/api/ledger/drain"] = Path(Operation("post", "drainLedgerQueue", "Ledger", "Drain ledger queue", "Runs one ledger drain cycle. Requires Ledger.Admin.", "LedgerDrainResponse", true)),
                    ["/v1.0/api/ledger/reconcile"] = Path(Operation("post", "reconcileLedger", "Ledger", "Reconcile ledger links", "Reports missing tenant and account ledger links visible to the caller.", "LedgerReconcileResponse", true)),
                    ["/v1.0/api/settings"] = Path(Operation("get", "getSettings", "Settings", "Get server settings", "Returns running server settings as a JSON settings object.", "XenoSettings", true), Operation("post", "updateSettings", "Settings", "Update server settings", "Persists a JSON settings object to the server config file and returns the saved settings object.", "XenoSettings", true)),
                    ["/v1.0/api/email/test"] = Path(Operation("post", "sendTestEmail", "Email", "Send test email", "Renders the supplied body with the standard Xeno email layout and sends it through the configured provider. Requires global/system administrator.", "EmailTestResponse", true)),
                    ["/v1.0/api/backup"] = Path(Operation("get", "backup", "Backup", "Backup", "Returns platform state backup.", "GenericObject", true)),
                    ["/v1.0/api/restore"] = Path(Operation("post", "restore", "Backup", "Restore", "Restores platform state backup.", "GenericObject", true)),
                    ["/v1/models"] = Path(Operation("get", "listOpenAiModels", "OpenAI", "List models", "OpenAI-compatible model list. Requires Inference.Read in the selected tenant.", "OpenAiModelList", true)),
                    ["/v1/chat/completions"] = Path(Operation("post", "createChatCompletion", "OpenAI", "Create chat completion", "OpenAI-compatible chat completion. Requires Inference.Execute in the selected tenant.", "OpenAiChatCompletion", true)),
                    ["/v1/completions"] = Path(Operation("post", "createCompletion", "OpenAI", "Create completion", "OpenAI-compatible completion. Requires Inference.Execute in the selected tenant.", "OpenAiChatCompletion", true)),
                    ["/v1/embeddings"] = Path(Operation("post", "createEmbedding", "OpenAI", "Create embeddings", "OpenAI-compatible embeddings. Requires Inference.Execute in the selected tenant.", "OpenAiEmbeddingList", true)),
                    ["/api/tags"] = Path(Operation("get", "listOllamaModels", "Ollama", "List models", "Ollama-compatible model list. Requires Inference.Read in the selected tenant.", "OllamaModelList", true)),
                    ["/api/chat"] = Path(Operation("post", "createOllamaChat", "Ollama", "Create chat completion", "Ollama-compatible chat completion. Requires Inference.Execute in the selected tenant.", "GenericObject", true)),
                    ["/api/generate"] = Path(Operation("post", "createOllamaGenerate", "Ollama", "Generate completion", "Ollama-compatible generation. Requires Inference.Execute in the selected tenant.", "GenericObject", true)),
                    ["/api/embed"] = Path(Operation("post", "createOllamaEmbed", "Ollama", "Generate embeddings", "Ollama-compatible embedding generation. Requires Inference.Execute in the selected tenant.", "GenericObject", true)),
                    ["/api/embeddings"] = Path(Operation("post", "createOllamaEmbeddings", "Ollama", "Generate embeddings", "Ollama-compatible legacy embedding generation. Requires Inference.Execute in the selected tenant.", "GenericObject", true))
                },
                components = new
                {
                    securitySchemes = new Dictionary<string, object>
                    {
                        ["bearerAuth"] = new { type = "http", scheme = "bearer", bearerFormat = "Xeno token", description = "Bearer session token or globally unique credential value. Bearer auth infers tenant context." },
                        ["xTokenAuth"] = new { type = "apiKey", @in = "header", name = "x-token", description = "Xeno bearer token supplied as an x-token header." },
                        ["accessKeyAuth"] = new { type = "apiKey", @in = "header", name = "x-access-key", description = "Tenant-bound Xeno credential access key." },
                        ["tenantHeader"] = new { type = "apiKey", @in = "header", name = "x-tenant-guid", description = "Optional tenant selector or compatibility hint. Bearer auth infers tenant context when omitted." },
                        ["tenantIdHeader"] = new { type = "apiKey", @in = "header", name = "x-tenant-id", description = "Alternate optional tenant identifier header." }
                    },
                    schemas = new Dictionary<string, object>
                    {
                        ["HealthResponse"] = ObjectSchema(Property("status", StringSchema()), Property("product", StringSchema()), Property("version", StringSchema())),
                        ["PingResponse"] = ObjectSchema(Property("status", StringSchema()), Property("product", StringSchema()), Property("version", StringSchema()), Property("sequence", IntegerSchema()), Property("clientSentUtc", StringSchema("date-time")), Property("serverUtc", StringSchema("date-time"))),
                        ["CredentialLoginResponse"] = ObjectSchema(Property("id", StringSchema()), Property("tenantId", StringSchema()), Property("userId", StringSchema()), Property("accessKey", StringSchema()), Property("authScheme", StringSchema())),
                        ["TokenResponse"] = ObjectSchema(Property("token", StringSchema()), Property("tokenType", StringSchema()), Property("tenantId", StringSchema()), Property("expiresUtc", StringSchema("date-time"))),
                        ["AuthOnboardingResponse"] = ObjectSchema(
                            Property("status", StringSchema()),
                            Property("tenant", SchemaRef("AuthOnboardingTenantDetails")),
                            Property("user", SchemaRef("AuthOnboardingUserDetails")),
                            Property("credential", SchemaRef("AuthOnboardingCredentialDetails")),
                            Property("token", SchemaRef("AuthOnboardingTokenDetails")),
                            Property("request", SchemaRef("AuthOnboardingRequestMetadata"))),
                        ["AuthOnboardingRequestMetadata"] = ObjectSchema(
                            Property("requestId", StringSchema()),
                            Property("correlationId", StringSchema()),
                            Property("method", StringSchema()),
                            Property("path", StringSchema()),
                            Property("sourceIp", StringSchema()),
                            Property("forwardedFor", StringSchema()),
                            Property("userAgent", StringSchema()),
                            Property("origin", StringSchema()),
                            Property("referer", StringSchema()),
                            Property("host", StringSchema()),
                            Property("eventUtc", StringSchema("date-time"))),
                        ["AuthOnboardingTenantDetails"] = ObjectSchema(
                            Property("id", StringSchema()),
                            Property("name", StringSchema()),
                            Property("active", BooleanSchema()),
                            Property("createdUtc", StringSchema("date-time")),
                            Property("lastUpdateUtc", StringSchema("date-time"))),
                        ["AuthOnboardingUserDetails"] = ObjectSchema(
                            Property("id", StringSchema()),
                            Property("tenantId", StringSchema()),
                            Property("username", StringSchema()),
                            Property("email", StringSchema()),
                            Property("isAdmin", BooleanSchema()),
                            Property("isTenantAdmin", BooleanSchema()),
                            Property("active", BooleanSchema())),
                        ["AuthOnboardingCredentialDetails"] = ObjectSchema(
                            Property("id", StringSchema()),
                            Property("tenantId", StringSchema()),
                            Property("userId", StringSchema()),
                            Property("accessKey", StringSchema()),
                            Property("secretKey", StringSchema()),
                            Property("active", BooleanSchema())),
                        ["AuthOnboardingTokenDetails"] = ObjectSchema(
                            Property("token", StringSchema()),
                            Property("tokenType", StringSchema()),
                            Property("tenantId", StringSchema()),
                            Property("userId", StringSchema()),
                            Property("sessionId", StringSchema()),
                            Property("expiresUtc", StringSchema("date-time"))),
                        ["AdminNukeResponse"] = ObjectSchema(
                            Property("status", StringSchema()),
                            Property("operationId", StringSchema()),
                            Property("operationType", StringSchema()),
                            Property("dryRun", BooleanSchema()),
                            Property("tenant", SchemaRef("AdminNukeTenantDetails")),
                            Property("user", SchemaRef("AdminNukeUserDetails")),
                            Property("requestedBy", SchemaRef("AdminNukeRequestedBy")),
                            Property("deleted", SchemaRef("AdminNukeCounts")),
                            Property("updated", SchemaRef("AdminNukeUpdatedCounts")),
                            Property("warnings", new Dictionary<string, object> { ["type"] = "array", ["items"] = StringSchema() }),
                            Property("startedUtc", StringSchema("date-time")),
                            Property("completedUtc", StringSchema("date-time"))),
                        ["AdminNukeTenantDetails"] = ObjectSchema(
                            Property("tenantId", StringSchema()),
                            Property("name", StringSchema()),
                            Property("status", StringSchema())),
                        ["AdminNukeUserDetails"] = ObjectSchema(
                            Property("userId", StringSchema()),
                            Property("email", StringSchema()),
                            Property("status", StringSchema())),
                        ["AdminNukeRequestedBy"] = ObjectSchema(
                            Property("tenantId", StringSchema()),
                            Property("userId", StringSchema()),
                            Property("isGlobalAdmin", BooleanSchema()),
                            Property("isTenantAdmin", BooleanSchema())),
                        ["AdminNukeCounts"] = ObjectSchema(
                            Property("tenants", IntegerSchema()),
                            Property("users", IntegerSchema()),
                            Property("credentials", IntegerSchema()),
                            Property("authSessions", IntegerSchema()),
                            Property("roles", IntegerSchema()),
                            Property("permissions", IntegerSchema()),
                            Property("roleAssignments", IntegerSchema()),
                            Property("auditRecords", IntegerSchema()),
                            Property("clients", IntegerSchema()),
                            Property("modelDefinitions", IntegerSchema()),
                            Property("modelConfigurations", IntegerSchema()),
                            Property("routingPolicies", IntegerSchema()),
                            Property("requestHistory", IntegerSchema()),
                            Property("telemetryRecords", IntegerSchema())),
                        ["AdminNukeUpdatedCounts"] = ObjectSchema(Property("requestHistoryAnonymized", IntegerSchema())),
                        ["TenantArray"] = ArraySchema("Tenant"),
                        ["UserArray"] = ArraySchema("User"),
                        ["CredentialArray"] = ArraySchema("Credential"),
                        ["RoleArray"] = ArraySchema("Role"),
                        ["PermissionArray"] = ArraySchema("Permission"),
                        ["RoleAssignmentArray"] = ArraySchema("RoleAssignment"),
                        ["EffectivePermissionArray"] = ArraySchema("EffectivePermission"),
                        ["AuditRecordArray"] = ArraySchema("AuditRecord"),
                        ["ClientArray"] = ArraySchema("XenoClientRecord"),
                        ["ServerClientTelemetrySampleRecordArray"] = ArraySchema("ServerClientTelemetrySampleRecord"),
                        ["ServerProviderTelemetrySampleRecordArray"] = ArraySchema("ServerProviderTelemetrySampleRecord"),
                        ["ServerClientTelemetryRollupBucketArray"] = ArraySchema("ServerClientTelemetryRollupBucket"),
                        ["ServerProviderTelemetryRollupBucketArray"] = ArraySchema("ServerProviderTelemetryRollupBucket"),
                        ["ServerClientTelemetryCollectionAttemptRecordArray"] = ArraySchema("ServerClientTelemetryCollectionAttemptRecord"),
                        ["RoutingPolicyMetricDefinitionArray"] = ArraySchema("RoutingPolicyMetricDefinition"),
                        ["ModelDefinitionArray"] = ArraySchema("ModelDefinition"),
                        ["ModelConfigurationArray"] = ArraySchema("ModelConfiguration"),
                        ["RoutingPolicyArray"] = ArraySchema("RoutingPolicy"),
                        ["RoutingPolicyValidationResult"] = ObjectSchema(
                            Property("valid", BooleanSchema()),
                            Property("issues", new Dictionary<string, object> { ["type"] = "array", ["items"] = SchemaRef("RoutingPolicyValidationIssue") })),
                        ["RoutingPolicyValidationIssue"] = ObjectSchema(
                            Property("field", StringSchema()),
                            Property("code", StringSchema()),
                            Property("message", StringSchema())),
                        ["RequestHistorySearchResult"] = ObjectSchema(
                            Property("items", ArraySchema("RequestHistoryEntry")),
                            Property("total", IntegerSchema()),
                            Property("page", IntegerSchema()),
                            Property("pageSize", IntegerSchema())),
                        ["ModelTokenUsage"] = ObjectSchema(
                            Property("requestCount", IntegerSchema()),
                            Property("totalTokens", IntegerSchema()),
                            Property("promptTokens", IntegerSchema()),
                            Property("outputTokens", IntegerSchema())),
                        ["ModelTokenLeaderboardResponse"] = ObjectSchema(
                            Property("models", new Dictionary<string, object> { ["type"] = "object", ["additionalProperties"] = SchemaRef("ModelTokenUsage") }),
                            Property("total", IntegerSchema()),
                            Property("limit", IntegerSchema()),
                            Property("offset", IntegerSchema())),
                        ["LedgerQueueStatus"] = ObjectSchema(
                            Property("enabled", BooleanSchema()),
                            Property("queueDepth", IntegerSchema()),
                            Property("oldestEventAgeMs", IntegerSchema()),
                            Property("lastSuccessfulDrainUtc", StringSchema("date-time")),
                            Property("lastFailureUtc", StringSchema("date-time")),
                            Property("lastFailure", StringSchema()),
                            Property("retryCount", IntegerSchema()),
                            Property("terminalFailureCount", IntegerSchema()),
                            Property("processedCount", IntegerSchema())),
                        ["LedgerStatusResponse"] = ObjectSchema(
                            Property("queue", SchemaRef("LedgerQueueStatus")),
                            Property("netLedgerHealthy", BooleanSchema()),
                            Property("netLedgerMessage", StringSchema())),
                        ["LedgerBalanceResponse"] = ObjectSchema(
                            Property("tenantId", StringSchema()),
                            Property("userId", StringSchema()),
                            Property("netLedgerTenantId", StringSchema()),
                            Property("netLedgerAccountId", StringSchema()),
                            Property("balance", IntegerSchema()),
                            Property("totalDebits", IntegerSchema()),
                            Property("totalCredits", IntegerSchema()),
                            Property("lastActivityUtc", StringSchema("date-time"))),
                        ["LedgerBalanceSearchResult"] = ObjectSchema(
                            Property("items", ArraySchema("LedgerBalanceResponse")),
                            Property("total", IntegerSchema()),
                            Property("page", IntegerSchema()),
                            Property("pageSize", IntegerSchema())),
                        ["LedgerEntryRecord"] = ObjectSchema(
                            Property("netLedgerEntryId", StringSchema()),
                            Property("ledgerEventId", StringSchema()),
                            Property("tenantId", StringSchema()),
                            Property("userId", StringSchema()),
                            Property("counterpartyUserId", StringSchema()),
                            Property("requestHistoryId", StringSchema()),
                            Property("selectedClientId", StringSchema()),
                            Property("direction", StringSchema()),
                            Property("amount", IntegerSchema()),
                            Property("committed", BooleanSchema()),
                            Property("labels", new Dictionary<string, object> { ["type"] = "array", ["items"] = StringSchema() }),
                            Property("tags", ObjectSchema()),
                            Property("occurredUtc", StringSchema("date-time")),
                            Property("createdUtc", StringSchema("date-time"))),
                        ["LedgerEntrySearchResult"] = ObjectSchema(
                            Property("items", ArraySchema("LedgerEntryRecord")),
                            Property("continuationToken", StringSchema()),
                            Property("total", IntegerSchema())),
                        ["LedgerDrainResponse"] = ObjectSchema(
                            Property("processed", IntegerSchema()),
                            Property("retried", IntegerSchema()),
                            Property("terminalFailures", IntegerSchema())),
                        ["LedgerReconcileResponse"] = ObjectSchema(
                            Property("missingTenantLinks", IntegerSchema()),
                            Property("missingAccountLinks", IntegerSchema()),
                            Property("messages", new Dictionary<string, object> { ["type"] = "array", ["items"] = StringSchema() })),
                        ["RequestHistorySummary"] = ObjectSchema(Property("buckets", new Dictionary<string, object> { ["type"] = "array", ["items"] = ObjectSchema() })),
                        ["RoutingDecision"] = ObjectSchema(Property("success", BooleanSchema()), Property("outcomeCode", StringSchema()), Property("message", StringSchema())),
                        ["OpenAiModelList"] = ObjectSchema(Property("object", StringSchema()), Property("data", new Dictionary<string, object> { ["type"] = "array", ["items"] = ObjectSchema() })),
                        ["OpenAiChatCompletion"] = ObjectSchema(Property("id", StringSchema()), Property("object", StringSchema()), Property("model", StringSchema())),
                        ["OpenAiEmbeddingList"] = ObjectSchema(Property("object", StringSchema()), Property("model", StringSchema())),
                        ["OllamaModelList"] = ObjectSchema(Property("models", new Dictionary<string, object> { ["type"] = "array", ["items"] = ObjectSchema() })),
                        ["ErrorResponse"] = ObjectSchema(Property("error", StringSchema()), Property("message", StringSchema())),
                        ["XenoSettings"] = ObjectSchema(
                            Property("Rest", ObjectSchema()),
                            Property("Cors", ObjectSchema()),
                            Property("WebSocket", ObjectSchema()),
                            Property("Database", ObjectSchema()),
                            Property("Logging", ObjectSchema()),
                            Property("Auth", ObjectSchema()),
                            Property("RequestHistory", ObjectSchema()),
                            Property("Routing", ObjectSchema()),
                            Property("TelemetryCollection", ObjectSchema()),
                            Property("Ledger", ObjectSchema()),
                            Property("Email", ObjectSchema())),
                        ["EmailTestResponse"] = ObjectSchema(
                            Property("accepted", BooleanSchema()),
                            Property("skipped", BooleanSchema()),
                            Property("provider", StringSchema()),
                            Property("providerMessageId", StringSchema()),
                            Property("statusMessage", StringSchema()),
                            Property("templateId", StringSchema()),
                            Property("toEmail", StringSchema()),
                            Property("subject", StringSchema()),
                            Property("sentUtc", StringSchema("date-time"))),
                        ["GenericObject"] = ObjectSchema(),
                        ["Tenant"] = ObjectSchema(Property("id", StringSchema()), Property("name", StringSchema())),
                        ["User"] = ObjectSchema(Property("id", StringSchema()), Property("tenantId", StringSchema()), Property("email", StringSchema())),
                        ["Credential"] = ObjectSchema(Property("id", StringSchema()), Property("tenantId", StringSchema()), Property("accessKey", StringSchema())),
                        ["Role"] = ObjectSchema(Property("id", StringSchema()), Property("tenantId", StringSchema()), Property("name", StringSchema())),
                        ["Permission"] = ObjectSchema(Property("id", StringSchema()), Property("tenantId", StringSchema()), Property("resourceType", StringSchema()), Property("operationType", StringSchema()), Property("permit", BooleanSchema()), Property("builtIn", BooleanSchema())),
                        ["RoleAssignment"] = ObjectSchema(Property("id", StringSchema()), Property("tenantId", StringSchema()), Property("roleId", StringSchema()), Property("principalType", StringSchema()), Property("principalId", StringSchema()), Property("active", BooleanSchema())),
                        ["EffectivePermission"] = ObjectSchema(Property("resourceType", StringSchema()), Property("sourceRole", StringSchema())),
                        ["AuditRecord"] = ObjectSchema(
                            Property("id", StringSchema()),
                            Property("tenantId", StringSchema()),
                            Property("targetTenantId", StringSchema()),
                            Property("targetResourceId", StringSchema()),
                            Property("requestId", StringSchema()),
                            Property("resourceType", StringSchema()),
                            Property("operationType", StringSchema()),
                            Property("allowed", BooleanSchema()),
                            Property("reasonCode", StringSchema()),
                            Property("sourceIp", StringSchema()),
                            Property("forwardedFor", StringSchema()),
                            Property("userAgent", StringSchema()),
                            Property("origin", StringSchema()),
                            Property("referer", StringSchema()),
                            Property("host", StringSchema()),
                            Property("requestMethod", StringSchema()),
                            Property("requestPath", StringSchema()),
                            Property("correlationId", StringSchema()),
                            Property("statusCode", IntegerSchema()),
                            Property("createdUtc", StringSchema("date-time"))),
                        ["XenoClientRecord"] = ObjectSchema(Property("id", StringSchema()), Property("tenantId", StringSchema()), Property("name", StringSchema()), Property("ownerUserId", StringSchema()), Property("ownerCredentialId", StringSchema()), Property("ownerPrincipalType", StringSchema()), Property("connected", BooleanSchema()), Property("models", new Dictionary<string, object> { ["type"] = "array", ["items"] = ObjectSchema() })),
                        ["ServerClientTelemetryCollectionStatusResponse"] = ObjectSchema(
                            Property("enabled", BooleanSchema()),
                            Property("running", BooleanSchema()),
                            Property("collectionIntervalSeconds", IntegerSchema()),
                            Property("requestTimeoutMs", IntegerSchema()),
                            Property("retentionDays", IntegerSchema()),
                            Property("lastCycleUtc", StringSchema("date-time")),
                            Property("nextCycleUtc", StringSchema("date-time")),
                            Property("connectedClientCount", IntegerSchema()),
                            Property("eligibleClientCount", IntegerSchema()),
                            Property("recentFailureCount", IntegerSchema())),
                        ["ServerClientTelemetryCollectResponse"] = ObjectSchema(
                            Property("attemptIds", new Dictionary<string, object> { ["type"] = "array", ["items"] = StringSchema() }),
                            Property("targetClientCount", IntegerSchema())),
                        ["ServerClientTelemetryLatestResponse"] = ObjectSchema(
                            Property("hostTelemetry", ArraySchema("ServerClientTelemetrySampleRecord")),
                            Property("providerTelemetry", ArraySchema("ServerProviderTelemetrySampleRecord"))),
                        ["ServerClientTelemetrySampleRecord"] = ObjectSchema(
                            Property("id", StringSchema()),
                            Property("tenantId", StringSchema()),
                            Property("clientId", StringSchema()),
                            Property("clientName", StringSchema()),
                            Property("machineName", StringSchema()),
                            Property("collectedUtc", StringSchema("date-time")),
                            Property("persistedUtc", StringSchema("date-time")),
                            Property("cpuUtilizationPercent", NumberSchema()),
                            Property("memoryUtilizationPercent", NumberSchema()),
                            Property("diskUtilizationPercent", NumberSchema()),
                            Property("gpuDeviceCount", IntegerSchema()),
                            Property("gpuAverageUtilizationPercent", NumberSchema())),
                        ["ServerClientTelemetrySampleDetail"] = ObjectSchema(
                            Property("sample", SchemaRef("ServerClientTelemetrySampleRecord")),
                            Property("gpuSamples", new Dictionary<string, object> { ["type"] = "array", ["items"] = ObjectSchema() }),
                            Property("rawSnapshotJson", StringSchema())),
                        ["ServerClientTelemetrySearchResult"] = ObjectSchema(
                            Property("items", ArraySchema("ServerClientTelemetrySampleRecord")),
                            Property("total", IntegerSchema()),
                            Property("page", IntegerSchema()),
                            Property("pageSize", IntegerSchema())),
                        ["ServerClientTelemetryRollupBucket"] = ObjectSchema(
                            Property("bucketUtc", StringSchema("date-time")),
                            Property("minimum", NumberSchema()),
                            Property("average", NumberSchema()),
                            Property("maximum", NumberSchema()),
                            Property("sampleCount", IntegerSchema())),
                        ["ServerProviderTelemetrySampleRecord"] = ObjectSchema(
                            Property("id", StringSchema()),
                            Property("tenantId", StringSchema()),
                            Property("clientId", StringSchema()),
                            Property("clientName", StringSchema()),
                            Property("providerName", StringSchema()),
                            Property("providerKind", StringSchema()),
                            Property("status", StringSchema()),
                            Property("healthStatus", StringSchema()),
                            Property("collectedUtc", StringSchema("date-time")),
                            Property("persistedUtc", StringSchema("date-time")),
                            Property("activeRequests", NumberSchema()),
                            Property("queuedRequests", NumberSchema()),
                            Property("loadedModelCount", IntegerSchema()),
                            Property("latencyP95Ms", NumberSchema())),
                        ["ServerProviderTelemetrySampleDetail"] = ObjectSchema(
                            Property("sample", SchemaRef("ServerProviderTelemetrySampleRecord")),
                            Property("models", new Dictionary<string, object> { ["type"] = "array", ["items"] = ObjectSchema() }),
                            Property("metrics", new Dictionary<string, object> { ["type"] = "array", ["items"] = ObjectSchema() }),
                            Property("rawSnapshotJson", StringSchema())),
                        ["ServerProviderTelemetrySearchResult"] = ObjectSchema(
                            Property("items", ArraySchema("ServerProviderTelemetrySampleRecord")),
                            Property("total", IntegerSchema()),
                            Property("page", IntegerSchema()),
                            Property("pageSize", IntegerSchema())),
                        ["ServerProviderTelemetryRollupBucket"] = ObjectSchema(
                            Property("bucketUtc", StringSchema("date-time")),
                            Property("minimum", NumberSchema()),
                            Property("average", NumberSchema()),
                            Property("maximum", NumberSchema()),
                            Property("sampleCount", IntegerSchema())),
                        ["ServerClientTelemetryCollectionAttemptRecord"] = ObjectSchema(
                            Property("id", StringSchema()),
                            Property("tenantId", StringSchema()),
                            Property("clientId", StringSchema()),
                            Property("clientName", StringSchema()),
                            Property("startedUtc", StringSchema("date-time")),
                            Property("completedUtc", StringSchema("date-time")),
                            Property("elapsedMs", NumberSchema()),
                            Property("hostRequested", BooleanSchema()),
                            Property("providerRequested", BooleanSchema()),
                            Property("hostSucceeded", BooleanSchema()),
                            Property("providerSucceeded", BooleanSchema()),
                            Property("status", StringSchema()),
                            Property("errorCode", StringSchema()),
                            Property("errorMessage", StringSchema()),
                            Property("hostSampleId", StringSchema()),
                            Property("providerSampleCount", IntegerSchema())),
                        ["RoutingPolicyMetricDefinition"] = ObjectSchema(
                            Property("name", StringSchema()),
                            Property("valueType", StringSchema())),
                        ["ModelDefinition"] = ObjectSchema(Property("id", StringSchema()), Property("tenantId", StringSchema()), Property("name", StringSchema())),
                        ["ModelConfiguration"] = ObjectSchema(Property("id", StringSchema()), Property("tenantId", StringSchema()), Property("name", StringSchema())),
                        ["RoutingPolicy"] = ObjectSchema(
                            Property("id", StringSchema()),
                            Property("tenantId", StringSchema()),
                            Property("name", StringSchema()),
                            Property("modelName", StringSchema()),
                            Property("clientName", StringSchema()),
                            Property("requestTimeoutMs", IntegerSchema()),
                            Property("loadBalancingMode", StringSchema()),
                            Property("adaptive", ObjectSchema()),
                            Property("isDefault", BooleanSchema()),
                            Property("active", BooleanSchema()),
                            Property("createdUtc", StringSchema("date-time")),
                            Property("lastUpdateUtc", StringSchema("date-time"))),
                        ["RequestHistoryEntry"] = ObjectSchema(
                            Property("id", StringSchema()),
                            Property("tenantId", StringSchema()),
                            Property("requestorTenantId", StringSchema()),
                            Property("requestorPrincipalType", StringSchema()),
                            Property("requestorPrincipalId", StringSchema()),
                            Property("path", StringSchema()),
                            Property("method", StringSchema()),
                            Property("model", StringSchema()),
                            Property("selectedClientId", StringSchema()),
                            Property("statusCode", IntegerSchema()),
                            Property("durationMs", IntegerSchema()),
                            Property("createdUtc", StringSchema("date-time")),
                            Property("completedUtc", StringSchema("date-time")),
                            Property("requestHeaders", ObjectSchema()),
                            Property("responseHeaders", ObjectSchema()),
                            Property("requestBody", StringSchema()),
                            Property("responseBody", StringSchema()),
                            Property("requestBodyLength", IntegerSchema()),
                            Property("responseBodyLength", IntegerSchema()),
                            Property("requestBodyTruncated", BooleanSchema()),
                            Property("responseBodyTruncated", BooleanSchema()),
                            Property("telemetry", ObjectSchema()),
                            Property("timeline", new Dictionary<string, object> { ["type"] = "array", ["items"] = ObjectSchema() }),
                            Property("routingDecision", ObjectSchema()))
                    }
                }
            };
        }

        private static string SwaggerHtml()
        {
            return """
<!doctype html>
<html>
<head>
  <title>Xeno Swagger</title>
  <link rel="stylesheet" href="https://unpkg.com/swagger-ui-dist@5/swagger-ui.css">
  <style>
    body { margin: 0; background: #f7f8fb; color: #1f2937; font-family: Inter, Arial, sans-serif; }
    .xeno-auth-panel { display: grid; gap: 12px; padding: 16px 20px; border-bottom: 1px solid #d9dee8; background: #ffffff; }
    .xeno-auth-header { display: flex; align-items: baseline; justify-content: space-between; gap: 12px; }
    .xeno-auth-header h1 { margin: 0; font-size: 17px; }
    .xeno-auth-header p { margin: 0; color: #5f6b7a; font-size: 13px; }
    .xeno-auth-grid { display: grid; grid-template-columns: repeat(4, minmax(160px, 1fr)); gap: 10px; }
    .xeno-auth-grid label { display: grid; gap: 5px; color: #374151; font-size: 12px; font-weight: 700; }
    .xeno-auth-grid input { min-width: 0; height: 34px; border: 1px solid #cfd6e2; border-radius: 6px; padding: 0 9px; font: inherit; }
    .xeno-auth-actions { display: flex; align-items: center; gap: 8px; }
    .xeno-auth-actions button { height: 34px; border: 1px solid #cfd6e2; border-radius: 6px; background: #ffffff; color: #111827; padding: 0 12px; font-weight: 700; cursor: pointer; }
    .xeno-auth-actions button.primary { border-color: #2f6fec; background: #2f6fec; color: #ffffff; }
    #xeno-auth-status { color: #147a3d; font-size: 12px; font-weight: 700; }
    @media (max-width: 1100px) { .xeno-auth-grid { grid-template-columns: repeat(2, minmax(160px, 1fr)); } }
    @media (max-width: 640px) { .xeno-auth-grid { grid-template-columns: 1fr; } }
  </style>
</head>
<body>
  <section class="xeno-auth-panel">
    <div class="xeno-auth-header">
      <div>
        <h1>Xeno authentication</h1>
        <p>Values saved here are loaded into Swagger requests as Xeno auth headers.</p>
      </div>
      <span id="xeno-auth-status"></span>
    </div>
    <div class="xeno-auth-grid">
      <label>Tenant ID<input id="xeno-tenant-id" placeholder="ten_default" autocomplete="off"></label>
      <label>Bearer token<input id="xeno-bearer-token" placeholder="Bearer token or raw token" autocomplete="off"></label>
      <label>x-token<input id="xeno-token" placeholder="Optional x-token header" autocomplete="off"></label>
      <label>Access key<input id="xeno-access-key" placeholder="default" autocomplete="off"></label>
    </div>
    <div class="xeno-auth-actions">
      <button class="primary" id="xeno-auth-save" type="button">Save authentication</button>
      <button id="xeno-auth-clear" type="button">Clear</button>
    </div>
  </section>
  <div id="swagger-ui"></div>
  <script src="https://unpkg.com/swagger-ui-dist@5/swagger-ui-bundle.js"></script>
  <script>
    (function () {
      const storageKey = 'xeno.swagger.auth';
      const fields = {
        tenantId: 'xeno-tenant-id',
        bearerToken: 'xeno-bearer-token',
        xToken: 'xeno-token',
        accessKey: 'xeno-access-key'
      };

      function loadAuth() {
        try {
          return JSON.parse(localStorage.getItem(storageKey) || '{}') || {};
        } catch {
          return {};
        }
      }

      function setStatus(message) {
        const status = document.getElementById('xeno-auth-status');
        if (!status) return;
        status.textContent = message;
        if (message) window.setTimeout(() => { status.textContent = ''; }, 1800);
      }

      function fieldValue(id) {
        const field = document.getElementById(id);
        return field ? field.value.trim() : '';
      }

      function hydrateAuthInputs() {
        const auth = loadAuth();
        Object.keys(fields).forEach((key) => {
          const field = document.getElementById(fields[key]);
          if (field) field.value = auth[key] || '';
        });
      }

      function saveAuth() {
        const auth = {};
        Object.keys(fields).forEach((key) => { auth[key] = fieldValue(fields[key]); });
        localStorage.setItem(storageKey, JSON.stringify(auth));
        setStatus('Authentication saved');
      }

      function clearAuth() {
        localStorage.removeItem(storageKey);
        Object.values(fields).forEach((id) => {
          const field = document.getElementById(id);
          if (field) field.value = '';
        });
        setStatus('Authentication cleared');
      }

      function bearerHeader(value) {
        if (!value) return '';
        return /^Bearer\s+/i.test(value) ? value : 'Bearer ' + value;
      }

      function applyAuth(request) {
        const auth = loadAuth();
        request.headers = request.headers || {};
        if (auth.tenantId) request.headers['x-tenant-guid'] = auth.tenantId;
        if (auth.bearerToken) request.headers.Authorization = bearerHeader(auth.bearerToken);
        if (auth.xToken) request.headers['x-token'] = auth.xToken;
        if (auth.accessKey) request.headers['x-access-key'] = auth.accessKey;
        return request;
      }

      function preauthorize(ui) {
        const auth = loadAuth();
        if (!ui || !ui.preauthorizeApiKey) return;
        if (auth.bearerToken) ui.preauthorizeApiKey('bearerAuth', auth.bearerToken.replace(/^Bearer\s+/i, ''));
        if (auth.xToken) ui.preauthorizeApiKey('xTokenAuth', auth.xToken);
        if (auth.accessKey) ui.preauthorizeApiKey('accessKeyAuth', auth.accessKey);
        if (auth.tenantId) ui.preauthorizeApiKey('tenantHeader', auth.tenantId);
      }

      window.onload = function () {
        hydrateAuthInputs();
        document.getElementById('xeno-auth-save').addEventListener('click', saveAuth);
        document.getElementById('xeno-auth-clear').addEventListener('click', clearAuth);
        let ui;
        ui = SwaggerUIBundle({
          url: '/openapi.json',
          dom_id: '#swagger-ui',
          deepLinking: true,
          displayRequestDuration: true,
          persistAuthorization: true,
          tryItOutEnabled: true,
          requestInterceptor: applyAuth,
          onComplete: function () { preauthorize(ui); }
        });
        window.xenoSwaggerUi = ui;
      };
    })();
  </script>
</body>
</html>
""";
        }

        private static Dictionary<string, object> Path(params KeyValuePair<string, object>[] operations)
        {
            Dictionary<string, object> path = new Dictionary<string, object>();
            foreach (KeyValuePair<string, object> operation in operations) path[operation.Key] = operation.Value;
            return path;
        }

        private static KeyValuePair<string, object> Operation(string method, string operationId, string tag, string summary, string description, string responseSchema, bool auth)
        {
            Dictionary<string, object> operation = new Dictionary<string, object>
            {
                ["operationId"] = operationId,
                ["tags"] = new[] { tag },
                ["summary"] = summary,
                ["description"] = description,
                ["responses"] = new Dictionary<string, object>
                {
                    ["200"] = new Dictionary<string, object>
                    {
                        ["description"] = "OK",
                        ["content"] = new Dictionary<string, object>
                        {
                            ["application/json"] = new Dictionary<string, object>
                            {
                                ["schema"] = SchemaRef(responseSchema)
                            }
                        }
                    }
                }
            };

            List<object> parameters = ManualParametersForOperation(operationId, auth);
            if (parameters.Count > 0) operation["parameters"] = parameters;

            object? requestBody = ManualRequestBodyForOperation(operationId);
            if (requestBody != null) operation["requestBody"] = requestBody;

            if (auth)
            {
                operation["security"] = AuthSecurityRequirements();
                Dictionary<string, object> responses = (Dictionary<string, object>)operation["responses"];
                responses["401"] = ErrorResponse("Authentication required or invalid.");
                responses["403"] = ErrorResponse("Authenticated caller lacks permission for the requested tenant or operation.");
            }

            if (String.Equals(operationId, "signup", StringComparison.Ordinal))
            {
                Dictionary<string, object> responses = (Dictionary<string, object>)operation["responses"];
                responses["400"] = ErrorResponse("Validation failed.");
                responses["404"] = ErrorResponse("Supplied tenant ID was not found.");
                responses["409"] = ErrorResponse("Tenant inactive, email already exists, or username already exists.");
                responses["429"] = ErrorResponse("Rate limit exceeded.");
            }

            return new KeyValuePair<string, object>(method, operation);
        }

        private static List<object> ManualParametersForOperation(string operationId, bool auth)
        {
            List<object> parameters = new List<object>();
            if (auth || IsTenantAwareOperation(operationId))
            {
                parameters.Add(Parameter("tenantId", "query", false, "Optional tenant identifier. Defaults to ten_default.", StringSchema()));
            }

            if (RequiresIdPathParameter(operationId))
            {
                parameters.Add(Parameter("id", "path", true, "Route identifier.", StringSchema()));
            }

            if (String.Equals(operationId, "getLatestClientTelemetry", StringComparison.Ordinal)
                || String.Equals(operationId, "getLatestClientProviderTelemetry", StringComparison.Ordinal)
                || String.Equals(operationId, "listClientTelemetryCollectionAttempts", StringComparison.Ordinal))
            {
                parameters.Add(Parameter("clientId", "query", false, "Optional client identifier.", StringSchema()));
            }

            if (String.Equals(operationId, "summarizeRequestHistory", StringComparison.Ordinal))
            {
                parameters.Add(Parameter("fromUtc", "query", false, "Inclusive UTC start time.", StringSchema("date-time")));
                parameters.Add(Parameter("toUtc", "query", false, "Exclusive UTC end time.", StringSchema("date-time")));
                parameters.Add(Parameter("bucketMinutes", "query", false, "Bucket width in minutes.", IntegerSchema()));
            }

            if (String.Equals(operationId, "listRequestHistory", StringComparison.Ordinal))
            {
                parameters.Add(Parameter("page", "query", false, "One-based page number. Defaults to 1.", IntegerSchema()));
                parameters.Add(Parameter("pageSize", "query", false, "Number of entries per page. Defaults to 100 and is capped at 1000.", IntegerSchema()));
                parameters.Add(Parameter("fromUtc", "query", false, "Inclusive UTC start time.", StringSchema("date-time")));
                parameters.Add(Parameter("toUtc", "query", false, "Exclusive UTC end time.", StringSchema("date-time")));
            }

            if (String.Equals(operationId, "getModelTokenLeaderboard", StringComparison.Ordinal))
            {
                parameters.Add(Parameter("tenantId", "query", false, "Optional tenant identifier. When omitted, global model usage is returned.", StringSchema()));
                parameters.Add(Parameter("fromUtc", "query", false, "Inclusive UTC start time.", StringSchema("date-time")));
                parameters.Add(Parameter("toUtc", "query", false, "Exclusive UTC end time.", StringSchema("date-time")));
                parameters.Add(Parameter("limit", "query", false, "Maximum ranked models to return. Omitted or null defaults to 100 and is capped at 1000.", IntegerSchema()));
                parameters.Add(Parameter("offset", "query", false, "Number of ranked models to skip. Omitted or null defaults to 0.", IntegerSchema()));
            }

            if (String.Equals(operationId, "requestAnalyticsOverview", StringComparison.Ordinal)
                || String.Equals(operationId, "analyticsOverview", StringComparison.Ordinal))
            {
                parameters.Add(Parameter("fromUtc", "query", false, "Inclusive UTC start time.", StringSchema("date-time")));
                parameters.Add(Parameter("toUtc", "query", false, "Exclusive UTC end time.", StringSchema("date-time")));
            }

            if (String.Equals(operationId, "lookupLoginTenants", StringComparison.Ordinal))
            {
                parameters.Add(Parameter("email", "query", true, "Email address used to discover tenant login options.", StringSchema()));
            }

            if (String.Equals(operationId, "searchLedgerBalances", StringComparison.Ordinal))
            {
                parameters.Add(Parameter("userId", "query", false, "Optional Xeno user identifier.", StringSchema()));
                parameters.Add(Parameter("page", "query", false, "One-based page number. Defaults to 1.", IntegerSchema()));
                parameters.Add(Parameter("pageSize", "query", false, "Number of balances per page. Defaults to 100 and is capped at 1000.", IntegerSchema()));
            }

            if (String.Equals(operationId, "getLedgerUserBalance", StringComparison.Ordinal))
            {
                parameters.Add(Parameter("userId", "path", true, "Xeno user identifier.", StringSchema()));
            }

            return parameters;
        }

        private static bool IsTenantAwareOperation(string operationId)
        {
            return String.Equals(operationId, "login", StringComparison.Ordinal)
                || String.Equals(operationId, "createToken", StringComparison.Ordinal)
                || String.Equals(operationId, "listOpenAiModels", StringComparison.Ordinal)
                || String.Equals(operationId, "createChatCompletion", StringComparison.Ordinal)
                || String.Equals(operationId, "createCompletion", StringComparison.Ordinal)
                || String.Equals(operationId, "createEmbedding", StringComparison.Ordinal);
        }

        private static bool RequiresIdPathParameter(string operationId)
        {
            return operationId is "updateTenant"
                or "deleteTenant"
                or "updateUser"
                or "deleteUser"
                or "updateCredential"
                or "deleteCredential"
                or "updateRole"
                or "deleteRole"
                or "updatePermission"
                or "deletePermission"
                or "deleteRoleAssignment"
                or "deleteAuditRecord"
                or "deleteClient"
                or "getClientTelemetryDetail"
                or "deleteClientTelemetryDetail"
                or "getClientProviderTelemetryDetail"
                or "deleteClientProviderTelemetryDetail"
                or "getRequestHistory"
                or "deleteRequestHistory";
        }

        private static object? ManualRequestBodyForOperation(string operationId)
        {
            if (String.Equals(operationId, "login", StringComparison.Ordinal))
            {
                return JsonRequestBody(
                    "User login request.",
                    ObjectSchema(
                        Property("tenantId", StringSchema()),
                        Property("username", StringSchema()),
                        Property("email", StringSchema()),
                        Property("password", StringSchema())),
                    new Dictionary<string, object>
                    {
                        ["tenantId"] = Constants.DefaultTenantId,
                        ["username"] = "admin",
                        ["password"] = "password"
                    });
            }

            if (String.Equals(operationId, "lookupLoginTenantsByBody", StringComparison.Ordinal))
            {
                return JsonRequestBody(
                    "Tenant lookup request.",
                    ObjectSchema(Property("email", StringSchema())),
                    new Dictionary<string, object>
                    {
                        ["email"] = "admin@xeno"
                    });
            }

            if (String.Equals(operationId, "signup", StringComparison.Ordinal))
            {
                return JsonRequestBody(
                    "Onboarding signup request.",
                    ObjectSchema(
                        Property("tenantId", StringSchema()),
                        Property("tenantName", StringSchema()),
                        Property("email", StringSchema()),
                        Property("username", StringSchema()),
                        Property("password", StringSchema())),
                    new Dictionary<string, object>
                    {
                        ["tenantName"] = "Example Company",
                        ["email"] = "admin@example.com",
                        ["password"] = "password"
                    });
            }

            if (String.Equals(operationId, "createToken", StringComparison.Ordinal))
            {
                return JsonRequestBody(
                    "Credential token request.",
                    ObjectSchema(Property("accessKey", StringSchema())),
                    new Dictionary<string, object>
                    {
                        ["accessKey"] = "default"
                    });
            }

            if (String.Equals(operationId, "createTenant", StringComparison.Ordinal))
            {
                return JsonRequestBody(
                    "Tenant create request.",
                    ObjectSchema(Property("id", StringSchema()), Property("name", StringSchema()), Property("active", BooleanSchema())),
                    new Dictionary<string, object> { ["name"] = "Example Tenant", ["active"] = true });
            }

            if (String.Equals(operationId, "updateTenant", StringComparison.Ordinal))
            {
                return JsonRequestBody(
                    "Tenant update request.",
                    ObjectSchema(Property("name", StringSchema()), Property("active", BooleanSchema())),
                    new Dictionary<string, object> { ["name"] = "Example Tenant", ["active"] = true });
            }

            if (String.Equals(operationId, "createUser", StringComparison.Ordinal) || String.Equals(operationId, "updateUser", StringComparison.Ordinal))
            {
                return JsonRequestBody(
                    String.Equals(operationId, "createUser", StringComparison.Ordinal) ? "User create request." : "User update request.",
                    ObjectSchema(
                        Property("tenantId", StringSchema()),
                        Property("username", StringSchema()),
                        Property("email", StringSchema()),
                        Property("password", StringSchema()),
                        Property("isAdmin", BooleanSchema()),
                        Property("isTenantAdmin", BooleanSchema()),
                        Property("active", BooleanSchema())),
                    new Dictionary<string, object>
                    {
                        ["username"] = "operator",
                        ["email"] = "operator@example.com",
                        ["password"] = "password",
                        ["isAdmin"] = false,
                        ["isTenantAdmin"] = false,
                        ["active"] = true
                    });
            }

            if (String.Equals(operationId, "createCredential", StringComparison.Ordinal) || String.Equals(operationId, "updateCredential", StringComparison.Ordinal))
            {
                return JsonRequestBody(
                    String.Equals(operationId, "createCredential", StringComparison.Ordinal) ? "Credential create request." : "Credential update request.",
                    ObjectSchema(
                        Property("tenantId", StringSchema()),
                        Property("userId", StringSchema()),
                        Property("accessKey", StringSchema()),
                        Property("active", BooleanSchema())),
                    new Dictionary<string, object>
                    {
                        ["userId"] = "usr_example",
                        ["accessKey"] = "default",
                        ["active"] = true
                    });
            }

            if (String.Equals(operationId, "createRole", StringComparison.Ordinal) || String.Equals(operationId, "updateRole", StringComparison.Ordinal))
            {
                return JsonRequestBody(
                    String.Equals(operationId, "createRole", StringComparison.Ordinal) ? "Role create request." : "Role update request.",
                    ObjectSchema(
                        Property("name", StringSchema()),
                        Property("permissionIds", new Dictionary<string, object> { ["type"] = "array", ["items"] = StringSchema() }),
                        Property("active", BooleanSchema())),
                    new Dictionary<string, object> { ["name"] = "Operators", ["permissionIds"] = Array.Empty<string>(), ["active"] = true });
            }

            if (String.Equals(operationId, "createPermission", StringComparison.Ordinal) || String.Equals(operationId, "updatePermission", StringComparison.Ordinal))
            {
                return JsonRequestBody(
                    String.Equals(operationId, "createPermission", StringComparison.Ordinal) ? "Permission create request." : "Permission update request.",
                    ObjectSchema(
                        Property("resourceType", StringSchema()),
                        Property("operationType", StringEnumSchema("Read", "Write", "Delete", "Admin", "Execute")),
                        Property("permit", BooleanSchema()),
                        Property("active", BooleanSchema())),
                    new Dictionary<string, object> { ["resourceType"] = "Clients", ["operationType"] = "Read", ["permit"] = true, ["active"] = true });
            }

            if (String.Equals(operationId, "createRoleAssignment", StringComparison.Ordinal))
            {
                return JsonRequestBody(
                    "Role assignment create request.",
                    ObjectSchema(
                        Property("tenantId", StringSchema()),
                        Property("roleId", StringSchema()),
                        Property("principalType", StringEnumSchema("User", "Credential")),
                        Property("principalId", StringSchema()),
                        Property("active", BooleanSchema())),
                    new Dictionary<string, object> { ["roleId"] = "role_example", ["principalType"] = "User", ["principalId"] = "usr_example", ["active"] = true });
            }

            if (String.Equals(operationId, "deleteClientModel", StringComparison.Ordinal))
            {
                return JsonRequestBody(
                    "Client-advertised model delete request.",
                    ObjectSchema(Property("clientId", StringSchema()), Property("name", StringSchema()), Property("source", StringSchema())),
                    new Dictionary<string, object> { ["clientId"] = "xcl_example", ["name"] = "llama3.2:latest", ["source"] = "local-ollama" });
            }

            if (String.Equals(operationId, "collectClientTelemetry", StringComparison.Ordinal) || String.Equals(operationId, "collectClientProviderTelemetry", StringComparison.Ordinal))
            {
                return JsonRequestBody(
                    "Manual telemetry collection request.",
                    ObjectSchema(
                        Property("clientId", StringSchema()),
                        Property("collectHostTelemetry", BooleanSchema()),
                        Property("collectProviderTelemetry", BooleanSchema()),
                        Property("wait", BooleanSchema())),
                    new Dictionary<string, object>
                    {
                        ["clientId"] = "xcl_example",
                        ["collectHostTelemetry"] = !String.Equals(operationId, "collectClientProviderTelemetry", StringComparison.Ordinal),
                        ["collectProviderTelemetry"] = true,
                        ["wait"] = false
                    });
            }

            if (String.Equals(operationId, "searchClientTelemetry", StringComparison.Ordinal))
            {
                return JsonRequestBody(
                    "Host telemetry search request.",
                    TelemetrySearchBodySchema(),
                    new Dictionary<string, object>
                    {
                        ["clientId"] = "xcl_example",
                        ["metric"] = "telemetry.cpu.utilizationPercent",
                        ["fromUtc"] = DateTime.UtcNow.AddHours(-1).ToString("O", CultureInfo.InvariantCulture),
                        ["toUtc"] = DateTime.UtcNow.ToString("O", CultureInfo.InvariantCulture),
                        ["page"] = 1,
                        ["pageSize"] = 50
                    });
            }

            if (String.Equals(operationId, "searchClientProviderTelemetry", StringComparison.Ordinal))
            {
                return JsonRequestBody(
                    "Provider telemetry search request.",
                    TelemetrySearchBodySchema(
                        Property("providerName", StringSchema()),
                        Property("modelName", StringSchema()),
                        Property("status", StringSchema()),
                        Property("healthStatus", StringSchema())),
                    new Dictionary<string, object>
                    {
                        ["clientId"] = "xcl_example",
                        ["providerName"] = "local-ollama",
                        ["metric"] = "provider.queuedRequests",
                        ["fromUtc"] = DateTime.UtcNow.AddHours(-1).ToString("O", CultureInfo.InvariantCulture),
                        ["toUtc"] = DateTime.UtcNow.ToString("O", CultureInfo.InvariantCulture),
                        ["page"] = 1,
                        ["pageSize"] = 50
                    });
            }

            if (String.Equals(operationId, "rollupClientTelemetry", StringComparison.Ordinal) || String.Equals(operationId, "rollupClientProviderTelemetry", StringComparison.Ordinal))
            {
                return JsonRequestBody(
                    "Telemetry rollup request.",
                    ObjectSchema(
                        Property("clientId", StringSchema()),
                        Property("providerName", StringSchema()),
                        Property("modelName", StringSchema()),
                        Property("metric", StringSchema()),
                        Property("fromUtc", StringSchema("date-time")),
                        Property("toUtc", StringSchema("date-time")),
                        Property("bucketMinutes", IntegerSchema())),
                    new Dictionary<string, object>
                    {
                        ["metric"] = String.Equals(operationId, "rollupClientTelemetry", StringComparison.Ordinal) ? "telemetry.cpu.utilizationPercent" : "provider.queuedRequests",
                        ["fromUtc"] = DateTime.UtcNow.AddHours(-1).ToString("O", CultureInfo.InvariantCulture),
                        ["toUtc"] = DateTime.UtcNow.ToString("O", CultureInfo.InvariantCulture),
                        ["bucketMinutes"] = 5
                    });
            }

            if (String.Equals(operationId, "bulkDeleteClientTelemetry", StringComparison.Ordinal) || String.Equals(operationId, "bulkDeleteClientProviderTelemetry", StringComparison.Ordinal))
            {
                return JsonRequestBody(
                    "Telemetry bulk delete request.",
                    ObjectSchema(
                        Property("clientId", StringSchema()),
                        Property("fromUtc", StringSchema("date-time")),
                        Property("toUtc", StringSchema("date-time"))),
                    new Dictionary<string, object>
                    {
                        ["clientId"] = "xcl_example",
                        ["toUtc"] = DateTime.UtcNow.AddDays(-30).ToString("O", CultureInfo.InvariantCulture)
                    });
            }

            if (String.Equals(operationId, "bulkDeleteRequestHistory", StringComparison.Ordinal))
            {
                return JsonRequestBody(
                    "Bulk request-history delete request.",
                    ObjectSchema(
                        Property("ids", new Dictionary<string, object> { ["type"] = "array", ["items"] = StringSchema() }),
                        Property("id", StringSchema()),
                        Property("model", StringSchema()),
                        Property("path", StringSchema()),
                        Property("selectedClientId", StringSchema())),
                    new Dictionary<string, object> { ["ids"] = new[] { "req_example" }, ["model"] = "llama3.2:latest", ["path"] = "/v1/chat/completions" });
            }

            if (String.Equals(operationId, "queryModelTokenLeaderboard", StringComparison.Ordinal))
            {
                return JsonRequestBody(
                    "Model token leaderboard request.",
                    ObjectSchema(
                        Property("tenantId", StringSchema()),
                        Property("fromUtc", StringSchema("date-time")),
                        Property("toUtc", StringSchema("date-time")),
                        Property("limit", IntegerSchema()),
                        Property("offset", IntegerSchema())),
                    new Dictionary<string, object>
                    {
                        ["fromUtc"] = DateTime.UtcNow.AddDays(-7).ToString("O", CultureInfo.InvariantCulture),
                        ["toUtc"] = DateTime.UtcNow.ToString("O", CultureInfo.InvariantCulture),
                        ["limit"] = 10,
                        ["offset"] = 0
                    });
            }

            if (String.Equals(operationId, "searchLedgerEntries", StringComparison.Ordinal))
            {
                return JsonRequestBody(
                    "Ledger entry search request.",
                    ObjectSchema(
                        Property("tenantId", StringSchema()),
                        Property("userId", StringSchema()),
                        Property("direction", StringEnumSchema("Debit", "Credit")),
                        Property("fromUtc", StringSchema("date-time")),
                        Property("toUtc", StringSchema("date-time")),
                        Property("requestHistoryId", StringSchema()),
                        Property("selectedClientId", StringSchema()),
                        Property("continuationToken", StringSchema()),
                        Property("pageSize", IntegerSchema())),
                    new Dictionary<string, object> { ["userId"] = "usr_example", ["direction"] = "Debit", ["pageSize"] = 100 });
            }

            if (String.Equals(operationId, "updateSettings", StringComparison.Ordinal))
            {
                return JsonRequestBody(
                    "Server settings update request.",
                    ObjectSchema(
                        Property("Rest", ObjectSchema()),
                        Property("Cors", ObjectSchema()),
                        Property("Logging", ObjectSchema()),
                        Property("WebSocket", ObjectSchema()),
                        Property("Database", ObjectSchema()),
                        Property("Auth", ObjectSchema()),
                        Property("RequestHistory", ObjectSchema()),
                        Property("Routing", ObjectSchema()),
                        Property("TelemetryCollection", ObjectSchema()),
                        Property("Ledger", ObjectSchema()),
                        Property("Email", ObjectSchema())),
                    new Dictionary<string, object>
                    {
                        ["Rest"] = new Dictionary<string, object> { ["Hostname"] = "0.0.0.0", ["Port"] = 9000, ["Ssl"] = false },
                        ["Cors"] = new Dictionary<string, object> { ["Enabled"] = true, ["AllowedOrigins"] = new[] { "*" } },
                        ["WebSocket"] = new Dictionary<string, object> { ["Enabled"] = true, ["Path"] = "/ws/v1/clients" },
                        ["Email"] = new Dictionary<string, object>
                        {
                            ["Enabled"] = false,
                            ["Provider"] = "Disabled",
                            ["FromAddress"] = "noreply@xenocloud.ai",
                            ["FromName"] = "Xeno",
                            ["ReplyToAddress"] = "support@xenocloud.ai",
                            ["SupportAddress"] = "support@xenocloud.ai",
                            ["ApplicationBaseUrl"] = "https://app.xenocloud.ai",
                            ["Mailgun"] = new Dictionary<string, object> { ["Domain"] = "", ["ApiKey"] = "", ["Region"] = "US" },
                            ["File"] = new Dictionary<string, object> { ["Directory"] = "data/email-outbox" },
                            ["Notifications"] = new Dictionary<string, object>
                            {
                                ["NotifyPlatformAdministratorsOnTenantCreated"] = true,
                                ["NotifyPlatformAdministratorsOnUserJoined"] = true,
                                ["NotifyTenantAdministratorsOnUserJoined"] = true,
                                ["NotifyNewTenantAdministrator"] = true,
                                ["NotifyNewTenantMember"] = false,
                                ["PlatformAdministratorRecipients"] = Array.Empty<object>()
                            }
                        }
                    });
            }

            if (String.Equals(operationId, "sendTestEmail", StringComparison.Ordinal))
            {
                return JsonRequestBody(
                    "Administrative test email request.",
                    ObjectSchema(
                        Property("toEmail", StringSchema()),
                        Property("toName", StringSchema()),
                        Property("title", StringSchema()),
                        Property("subject", StringSchema()),
                        Property("body", StringSchema())),
                    new Dictionary<string, object>
                    {
                        ["toEmail"] = "joel.christner@gmail.com",
                        ["title"] = "Xeno Test Email",
                        ["subject"] = "Xeno test email",
                        ["body"] = "hello world"
                    });
            }

            if (String.Equals(operationId, "restore", StringComparison.Ordinal))
            {
                return JsonRequestBody(
                    "Platform-state restore request.",
                    ObjectSchema(
                        Property("tenants", new Dictionary<string, object> { ["type"] = "array", ["items"] = ObjectSchema() }),
                        Property("users", new Dictionary<string, object> { ["type"] = "array", ["items"] = ObjectSchema() }),
                        Property("credentials", new Dictionary<string, object> { ["type"] = "array", ["items"] = ObjectSchema() })),
                    new Dictionary<string, object> { ["tenants"] = Array.Empty<object>(), ["users"] = Array.Empty<object>(), ["credentials"] = Array.Empty<object>() });
            }

            if (String.Equals(operationId, "createChatCompletion", StringComparison.Ordinal))
            {
                return JsonRequestBody(
                    "OpenAI-compatible chat completion request.",
                    ObjectSchema(
                        Property("model", StringSchema()),
                        Property("messages", new Dictionary<string, object>
                        {
                            ["type"] = "array",
                            ["items"] = ObjectSchema(Property("role", StringSchema()), Property("content", StringSchema()))
                        }),
                        Property("temperature", NumberSchema()),
                        Property("max_tokens", IntegerSchema()),
                        Property("max_output_tokens", IntegerSchema()),
                        Property("stream", BooleanSchema()),
                        Property("stream_options", ObjectSchema(Property("include_usage", BooleanSchema())))),
                    new Dictionary<string, object>
                    {
                        ["model"] = "gpt-oss:120b",
                        ["messages"] = new[]
                        {
                            new Dictionary<string, object> { ["role"] = "system", ["content"] = "You are a helpful and concise assistant." },
                            new Dictionary<string, object> { ["role"] = "user", ["content"] = "Hello! Can you give me a brief explanation of how an inverted index works?" }
                        },
                        ["temperature"] = 0.7,
                        ["max_output_tokens"] = 2000,
                        ["stream"] = true,
                        ["stream_options"] = new Dictionary<string, object>
                        {
                            ["include_usage"] = true
                        }
                    });
            }

            if (String.Equals(operationId, "createCompletion", StringComparison.Ordinal))
            {
                return JsonRequestBody(
                    "OpenAI-compatible text completion request.",
                    ObjectSchema(
                        Property("model", StringSchema()),
                        Property("prompt", StringSchema()),
                        Property("temperature", NumberSchema()),
                        Property("max_tokens", IntegerSchema()),
                        Property("max_output_tokens", IntegerSchema()),
                        Property("stream", BooleanSchema()),
                        Property("stream_options", ObjectSchema(Property("include_usage", BooleanSchema())))),
                    new Dictionary<string, object>
                    {
                        ["model"] = "gpt-oss:120b",
                        ["prompt"] = "Explain how an inverted index works in two concise paragraphs.",
                        ["temperature"] = 0.7,
                        ["max_output_tokens"] = 2000,
                        ["stream"] = true,
                        ["stream_options"] = new Dictionary<string, object>
                        {
                            ["include_usage"] = true
                        }
                    });
            }

            if (String.Equals(operationId, "createEmbedding", StringComparison.Ordinal))
            {
                return JsonRequestBody(
                    "OpenAI-compatible embeddings request.",
                    ObjectSchema(
                        Property("model", StringSchema()),
                        Property("input", new Dictionary<string, object> { ["type"] = "array", ["items"] = StringSchema() })),
                    new Dictionary<string, object>
                    {
                        ["model"] = "all-minilm",
                        ["input"] = new[]
                        {
                            "The quick brown fox jumps over the lazy dog.",
                            "Artificial intelligence is transforming software development.",
                            "How do I cook a perfect medium-rare steak?"
                        }
                    });
            }

            if (String.Equals(operationId, "createOllamaChat", StringComparison.Ordinal))
            {
                return JsonRequestBody(
                    "Ollama-compatible chat request.",
                    ObjectSchema(
                        Property("model", StringSchema()),
                        Property("messages", new Dictionary<string, object>
                        {
                            ["type"] = "array",
                            ["items"] = ObjectSchema(Property("role", StringSchema()), Property("content", StringSchema()))
                        }),
                        Property("stream", BooleanSchema())),
                    new Dictionary<string, object>
                    {
                        ["model"] = "llama3.2:latest",
                        ["messages"] = new[]
                        {
                            new Dictionary<string, object> { ["role"] = "user", ["content"] = "Hello! Reply in one sentence." }
                        },
                        ["stream"] = false
                    });
            }

            if (String.Equals(operationId, "createOllamaGenerate", StringComparison.Ordinal))
            {
                return JsonRequestBody(
                    "Ollama-compatible generation request.",
                    ObjectSchema(
                        Property("model", StringSchema()),
                        Property("prompt", StringSchema()),
                        Property("stream", BooleanSchema())),
                    new Dictionary<string, object>
                    {
                        ["model"] = "llama3.2:latest",
                        ["prompt"] = "Say hello in one sentence.",
                        ["stream"] = false
                    });
            }

            if (String.Equals(operationId, "createOllamaEmbed", StringComparison.Ordinal) || String.Equals(operationId, "createOllamaEmbeddings", StringComparison.Ordinal))
            {
                return JsonRequestBody(
                    "Ollama-compatible embeddings request.",
                    ObjectSchema(
                        Property("model", StringSchema()),
                        Property("input", new Dictionary<string, object> { ["oneOf"] = new object[] { StringSchema(), new Dictionary<string, object> { ["type"] = "array", ["items"] = StringSchema() } } })),
                    new Dictionary<string, object>
                    {
                        ["model"] = "llama3.2:latest",
                        ["input"] = new[] { "hello", "goodbye" }
                    });
            }

            return null;
        }

        private static object[] AuthSecurityRequirements()
        {
            return new object[]
            {
                new Dictionary<string, object> { ["bearerAuth"] = Array.Empty<string>() },
                new Dictionary<string, object> { ["xTokenAuth"] = Array.Empty<string>() },
                new Dictionary<string, object>
                {
                    ["accessKeyAuth"] = Array.Empty<string>()
                }
            };
        }

        private static Dictionary<string, object> ErrorResponse(string description)
        {
            return new Dictionary<string, object>
            {
                ["description"] = description,
                ["content"] = new Dictionary<string, object>
                {
                    ["application/json"] = new Dictionary<string, object>
                    {
                        ["schema"] = SchemaRef("ErrorResponse")
                    }
                }
            };
        }

        private static Dictionary<string, object> Parameter(string name, string location, bool required, string description, object schema)
        {
            return new Dictionary<string, object>
            {
                ["name"] = name,
                ["in"] = location,
                ["required"] = required,
                ["description"] = description,
                ["schema"] = schema
            };
        }

        private static Dictionary<string, object> JsonRequestBody(string description, object schema, object example)
        {
            return new Dictionary<string, object>
            {
                ["description"] = description,
                ["required"] = true,
                ["content"] = new Dictionary<string, object>
                {
                    ["application/json"] = new Dictionary<string, object>
                    {
                        ["schema"] = schema,
                        ["example"] = example
                    }
                }
            };
        }

        private static Dictionary<string, object> ObjectSchema(params KeyValuePair<string, object>[] properties)
        {
            Dictionary<string, object> propertyMap = new Dictionary<string, object>();
            foreach (KeyValuePair<string, object> property in properties) propertyMap[property.Key] = property.Value;
            return new Dictionary<string, object> { ["type"] = "object", ["properties"] = propertyMap };
        }

        private static Dictionary<string, object> TelemetrySearchBodySchema(params KeyValuePair<string, object>[] additionalProperties)
        {
            List<KeyValuePair<string, object>> properties = new List<KeyValuePair<string, object>>
            {
                Property("clientId", StringSchema()),
                Property("clientName", StringSchema()),
                Property("machineName", StringSchema()),
                Property("metric", StringSchema()),
                Property("metricMinimum", NumberSchema()),
                Property("metricMaximum", NumberSchema()),
                Property("fromUtc", StringSchema("date-time")),
                Property("toUtc", StringSchema("date-time")),
                Property("page", IntegerSchema()),
                Property("pageSize", IntegerSchema()),
                Property("sortDescending", BooleanSchema())
            };
            properties.AddRange(additionalProperties);
            return ObjectSchema(properties.ToArray());
        }

        private static KeyValuePair<string, object> Property(string name, object schema)
        {
            return new KeyValuePair<string, object>(name, schema);
        }

        private static Dictionary<string, object> SchemaRef(string schemaName)
        {
            return new Dictionary<string, object> { ["$ref"] = "#/components/schemas/" + schemaName };
        }

        private static Dictionary<string, object> ArraySchema(string itemSchemaName)
        {
            return new Dictionary<string, object> { ["type"] = "array", ["items"] = SchemaRef(itemSchemaName) };
        }

        private static Dictionary<string, object> StringSchema(string? format = null)
        {
            Dictionary<string, object> schema = new Dictionary<string, object> { ["type"] = "string" };
            if (!String.IsNullOrWhiteSpace(format)) schema["format"] = format;
            return schema;
        }

        private static Dictionary<string, object> BooleanSchema()
        {
            return new Dictionary<string, object> { ["type"] = "boolean" };
        }

        private static Dictionary<string, object> NumberSchema()
        {
            return new Dictionary<string, object> { ["type"] = "number", ["format"] = "double" };
        }

        private static Dictionary<string, object> IntegerSchema()
        {
            return new Dictionary<string, object> { ["type"] = "integer", ["format"] = "int32" };
        }

        private static Dictionary<string, object> StringEnumSchema(params string[] values)
        {
            return new Dictionary<string, object> { ["type"] = "string", ["enum"] = values };
        }
    }
}
