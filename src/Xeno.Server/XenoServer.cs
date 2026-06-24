namespace Xeno.Server
{
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Linq;
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
    using Xeno.Core.Helpers;
    using Xeno.Core.Models;
    using Xeno.Core.Serialization;
    using Xeno.Core.Services;
    using Xeno.Core.Settings;

    /// <summary>
    /// Watson-backed Xeno REST, OpenAPI, Swagger, and WebSocket server host.
    /// </summary>
    public sealed class XenoServer : IDisposable
    {
        private readonly XenoSettings _Settings;
        private readonly string _SettingsFile;
        private readonly Webserver _Webserver;
        private readonly object _ClientConnectionsLock = new object();
        private readonly object _PendingResponsesLock = new object();
        private readonly Dictionary<string, ClientConnection> _ClientConnections = new Dictionary<string, ClientConnection>(StringComparer.OrdinalIgnoreCase);
        private readonly Dictionary<string, TaskCompletionSource<string>> _PendingClientResponses = new Dictionary<string, TaskCompletionSource<string>>(StringComparer.OrdinalIgnoreCase);
        private readonly AuthenticationService _Authentication = new AuthenticationService();
        private DatabaseDriverBase? _Database;
        private RoutingService? _Routing;
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

            WatsonWebserver.Core.WebserverSettings webserverSettings = new WatsonWebserver.Core.WebserverSettings(
                _Settings.Rest.Hostname,
                _Settings.Rest.Port,
                _Settings.Rest.Ssl);
            webserverSettings.WebSockets.Enable = _Settings.WebSocket.Enabled;

            _Webserver = new Webserver(webserverSettings, DefaultRouteAsync);
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
            _Routing = new RoutingService(_Database.Platform);

            _Webserver.Start();

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
            _Database?.Dispose();
            _Disposed = true;
        }

        private void ConfigureWatson()
        {
            _Webserver.UseOpenApi(openApi =>
            {
                openApi.EnableOpenApi = true;
                openApi.EnableSwaggerUi = true;
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
                openApi.Tags.Add(new OpenApiTag { Name = "Tenants", Description = "Tenant administration" });
                openApi.Tags.Add(new OpenApiTag { Name = "Clients", Description = "Connected Xeno clients and their advertised models" });
                openApi.Tags.Add(new OpenApiTag { Name = "Routing", Description = "Direct routing to connected Xeno clients" });
                openApi.Tags.Add(new OpenApiTag { Name = "OpenAI", Description = "OpenAI-compatible inference API" });
                openApi.Tags.Add(new OpenApiTag { Name = "Request History", Description = "Captured request history and summaries" });
                openApi.Tags.Add(new OpenApiTag { Name = "Settings", Description = "Server settings and JSON config persistence" });
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

        private void RegisterRestRoutes()
        {
            RegisterStaticRoute(HttpMethod.GET, "/", "Health check", "Health");
            RegisterStaticRoute(HttpMethod.GET, "/health", "Health check", "Health");
            RegisterStaticRoute(HttpMethod.GET, "/metrics", "Prometheus-style metrics", "Health");
            RegisterStaticRoute(HttpMethod.GET, "/v1.0/api/health", "Health check", "Health");
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
            RegisterStaticRoute(HttpMethod.GET, "/api/v1/tenants", "List tenants", "Tenants");
            RegisterStaticRoute(HttpMethod.GET, "/v1.0/api/tenants", "List tenants", "Tenants");
            RegisterStaticRoute(HttpMethod.GET, "/api/v1/users", "List users", "Tenants");
            RegisterStaticRoute(HttpMethod.GET, "/v1.0/api/users", "List users", "Tenants");
            RegisterStaticRoute(HttpMethod.GET, "/api/v1/credentials", "List credentials", "Tenants");
            RegisterStaticRoute(HttpMethod.GET, "/v1.0/api/credentials", "List credentials", "Tenants");
            RegisterStaticRoute(HttpMethod.POST, "/api/v1/credentials", "Create credential", "Tenants");
            RegisterStaticRoute(HttpMethod.POST, "/v1.0/api/credentials", "Create credential", "Tenants");
            RegisterStaticRoute(HttpMethod.GET, "/api/v1/roles", "List roles", "Authorization");
            RegisterStaticRoute(HttpMethod.GET, "/v1.0/api/roles", "List roles", "Authorization");
            RegisterStaticRoute(HttpMethod.GET, "/api/v1/permissions", "List permissions", "Authorization");
            RegisterStaticRoute(HttpMethod.GET, "/v1.0/api/permissions", "List permissions", "Authorization");
            RegisterStaticRoute(HttpMethod.GET, "/api/v1/effective-permissions", "List effective permissions", "Authorization");
            RegisterStaticRoute(HttpMethod.GET, "/v1.0/api/effective-permissions", "List effective permissions", "Authorization");
            RegisterStaticRoute(HttpMethod.GET, "/api/v1/audit", "List audit records", "Audit");
            RegisterStaticRoute(HttpMethod.GET, "/v1.0/api/audit", "List audit records", "Audit");
            RegisterStaticRoute(HttpMethod.GET, "/api/v1/clients", "List clients", "Clients");
            RegisterStaticRoute(HttpMethod.GET, "/v1.0/api/clients", "List clients", "Clients");
            RegisterStaticRoute(HttpMethod.DELETE, "/api/v1/clientmodels", "Delete client-advertised model", "Clients");
            RegisterStaticRoute(HttpMethod.DELETE, "/v1.0/api/client-models", "Delete client-advertised model", "Clients");
            RegisterStaticRoute(HttpMethod.GET, "/api/v1/modeldefinitions", "List model definitions", "Routing");
            RegisterStaticRoute(HttpMethod.GET, "/v1.0/api/model-definitions", "List model definitions", "Routing");
            RegisterStaticRoute(HttpMethod.GET, "/api/v1/modelconfigurations", "List model configurations", "Routing");
            RegisterStaticRoute(HttpMethod.GET, "/v1.0/api/model-configurations", "List model configurations", "Routing");
            RegisterStaticRoute(HttpMethod.GET, "/v1/models", "List OpenAI models", "OpenAI");
            RegisterStaticRoute(HttpMethod.GET, "/api/v1/openai/v1/models", "List OpenAI models", "OpenAI");
            RegisterStaticRoute(HttpMethod.POST, "/v1/chat/completions", "Create chat completion", "OpenAI");
            RegisterStaticRoute(HttpMethod.POST, "/v1/completions", "Create completion", "OpenAI");
            RegisterStaticRoute(HttpMethod.POST, "/api/v1/openai/v1/chat/completions", "Create chat completion", "OpenAI");
            RegisterStaticRoute(HttpMethod.POST, "/api/v1/openai/v1/completions", "Create completion", "OpenAI");
            RegisterStaticRoute(HttpMethod.POST, "/v1/embeddings", "Create embeddings", "OpenAI");
            RegisterStaticRoute(HttpMethod.POST, "/api/v1/openai/v1/embeddings", "Create embeddings", "OpenAI");
            RegisterStaticRoute(HttpMethod.GET, "/api/v1/requesthistory", "List request history", "Request History");
            RegisterStaticRoute(HttpMethod.GET, "/v1.0/api/request-history", "List request history", "Request History");
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
            RegisterStaticRoute(HttpMethod.GET, "/api/v1/backup", "Backup platform state", "Backup");
            RegisterStaticRoute(HttpMethod.GET, "/v1.0/api/backup", "Backup platform state", "Backup");
            RegisterStaticRoute(HttpMethod.POST, "/api/v1/restore", "Restore platform state", "Backup");
            RegisterStaticRoute(HttpMethod.POST, "/v1.0/api/restore", "Restore platform state", "Backup");
            RegisterParameterizedRoute(HttpMethod.DELETE, "/api/v1/tenants/{id}", "Delete tenant", "Tenants");
            RegisterParameterizedRoute(HttpMethod.DELETE, "/v1.0/api/tenants/{id}", "Delete tenant", "Tenants");
            RegisterParameterizedRoute(HttpMethod.DELETE, "/api/v1/users/{id}", "Delete user", "Tenants");
            RegisterParameterizedRoute(HttpMethod.DELETE, "/v1.0/api/users/{id}", "Delete user", "Tenants");
            RegisterParameterizedRoute(HttpMethod.DELETE, "/api/v1/credentials/{id}", "Delete credential", "Tenants");
            RegisterParameterizedRoute(HttpMethod.DELETE, "/v1.0/api/credentials/{id}", "Delete credential", "Tenants");
            RegisterParameterizedRoute(HttpMethod.DELETE, "/api/v1/roles/{id}", "Delete role", "Authorization");
            RegisterParameterizedRoute(HttpMethod.DELETE, "/v1.0/api/roles/{id}", "Delete role", "Authorization");
            RegisterParameterizedRoute(HttpMethod.DELETE, "/api/v1/permissions/{id}", "Delete permission", "Authorization");
            RegisterParameterizedRoute(HttpMethod.DELETE, "/v1.0/api/permissions/{id}", "Delete permission", "Authorization");
            RegisterParameterizedRoute(HttpMethod.DELETE, "/api/v1/audit/{id}", "Delete audit record", "Audit");
            RegisterParameterizedRoute(HttpMethod.DELETE, "/v1.0/api/audit/{id}", "Delete audit record", "Audit");
            RegisterParameterizedRoute(HttpMethod.DELETE, "/api/v1/clients/{id}", "Delete client", "Clients");
            RegisterParameterizedRoute(HttpMethod.DELETE, "/v1.0/api/clients/{id}", "Delete client", "Clients");
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
                openApiMetadata: OpenApiRouteMetadata.Create(summary, tag));
        }

        private void RegisterParameterizedRoute(HttpMethod method, string path, string summary, string tag)
        {
            _Webserver.Routes.PreAuthentication.Parameter.Add(
                method,
                path,
                DefaultRouteAsync,
                ExceptionRouteAsync,
                openApiMetadata: OpenApiRouteMetadata.Create(summary, tag));
        }

        private async Task PreflightRouteAsync(HttpContextBase context)
        {
            ApplyCors(context.Response);
            context.Response.StatusCode = 204;
            await context.Response.Send(context.Token).ConfigureAwait(false);
        }

        private async Task PostRoutingRouteAsync(HttpContextBase context)
        {
            ApplyCors(context.Response);
            await Task.CompletedTask.ConfigureAwait(false);
        }

        private async Task OpenApiRouteAsync(HttpContextBase context)
        {
            ApplyCors(context.Response);
            await SendJsonAsync(context, BuildOpenApi()).ConfigureAwait(false);
        }

        private async Task SwaggerRouteAsync(HttpContextBase context)
        {
            ApplyCors(context.Response);
            context.Response.ContentType = "text/html";
            await context.Response.Send(SwaggerHtml(), context.Token).ConfigureAwait(false);
        }

        private async Task ExceptionRouteAsync(HttpContextBase context, Exception exception)
        {
            ApplyCors(context.Response);
            context.Response.StatusCode = 500;
            await SendJsonAsync(context, new { error = "InternalServerError", message = exception.Message }).ConfigureAwait(false);
        }

        private async Task DefaultRouteAsync(HttpContextBase context)
        {
            string path = context.Request.Url.RawWithoutQuery ?? "/";
            string method = context.Request.Method.ToString().ToUpperInvariant();

            try
            {
                ApplyCors(context.Response);
                await HandleRestAsync(context, method, path, context.Token).ConfigureAwait(false);
            }
            catch (UnauthorizedAccessException ex)
            {
                context.Response.StatusCode = 401;
                await SendJsonAsync(context, new { error = "Unauthorized", message = ex.Message }).ConfigureAwait(false);
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
                await HandleLoginAsync(context, token).ConfigureAwait(false);
                return;
            }

            RequestContext requestContext = CreateRequestContext(context);

            if (method == "POST" && IsPath(path, "/api/v1/auth/token", "/v1.0/api/auth/token"))
            {
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

            if (method == "GET" && IsPath(path, "/api/v1/tenants", "/v1.0/api/tenants"))
            {
                await RequirePermissionAsync(requestContext, "Tenants", OperationTypeEnum.Read, token).ConfigureAwait(false);
                await SendJsonAsync(context, RequirePlatform().Tenants).ConfigureAwait(false);
                return;
            }

            if (method == "DELETE" && TryGetPathId(path, "/api/v1/tenants/", "/v1.0/api/tenants/") is string deleteTenantId)
            {
                await RequirePermissionAsync(requestContext, "Admin", OperationTypeEnum.Admin, token).ConfigureAwait(false);
                int removed = DeleteTenant(deleteTenantId);
                if (removed > 0) await SavePlatformAsync(token).ConfigureAwait(false);
                await SendNoContentAsync(context).ConfigureAwait(false);
                return;
            }

            if (method == "GET" && IsPath(path, "/api/v1/users", "/v1.0/api/users"))
            {
                await RequirePermissionAsync(requestContext, "Users", OperationTypeEnum.Read, token).ConfigureAwait(false);
                await SendJsonAsync(context, RequirePlatform().Users.Where(item => item.TenantId == requestContext.TenantId || requestContext.TenantId == Constants.DefaultTenantId).ToList()).ConfigureAwait(false);
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
                await SendJsonAsync(context, RequirePlatform().Credentials.Where(item => item.TenantId == requestContext.TenantId).Select(RedactCredential).ToList()).ConfigureAwait(false);
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
                    credential.SecretKey,
                    credential.Active,
                    warning = "SecretKey is shown once. Store it securely."
                }).ConfigureAwait(false);
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
                await RequirePermissionAsync(requestContext, "Admin", OperationTypeEnum.Admin, token).ConfigureAwait(false);
                await SendJsonAsync(context, RequirePlatform().Roles.Where(item => item.TenantId == requestContext.TenantId).ToList()).ConfigureAwait(false);
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
                await RequirePermissionAsync(requestContext, "Admin", OperationTypeEnum.Admin, token).ConfigureAwait(false);
                await SendJsonAsync(context, RequirePlatform().Permissions.Where(item => item.TenantId == requestContext.TenantId).ToList()).ConfigureAwait(false);
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

            if (method == "GET" && IsPath(path, "/v1/models", "/api/v1/openai/v1/models"))
            {
                List<object> models = RequirePlatform().Clients
                    .Where(item => item.TenantId == requestContext.TenantId)
                    .SelectMany(item => item.Models)
                    .GroupBy(item => item.Name, StringComparer.OrdinalIgnoreCase)
                    .Select(group => (object)new { id = group.Key, @object = "model", owned_by = "xeno" })
                    .ToList();
                await SendJsonAsync(context, new { @object = "list", data = models }).ConfigureAwait(false);
                return;
            }

            if (method == "POST" && IsPath(path, "/v1/chat/completions", "/v1/completions", "/api/v1/openai/v1/chat/completions", "/api/v1/openai/v1/completions"))
            {
                await HandleCompletionAsync(context, requestContext, path, false, token).ConfigureAwait(false);
                return;
            }

            if (method == "POST" && IsPath(path, "/v1/embeddings", "/api/v1/openai/v1/embeddings"))
            {
                await HandleEmbeddingAsync(context, requestContext, path, token).ConfigureAwait(false);
                return;
            }

            if (method == "GET" && IsPath(path, "/api/v1/requesthistory", "/v1.0/api/request-history"))
            {
                await SendJsonAsync(context, FilterRequestHistory(requestContext)).ConfigureAwait(false);
                return;
            }

            if (method == "GET" && IsPath(path, "/api/v1/requesthistory/summary", "/v1.0/api/request-history/summary"))
            {
                await SendJsonAsync(context, SummarizeRequestHistory(requestContext, context)).ConfigureAwait(false);
                return;
            }

            if (method == "GET" && IsPath(path, "/api/v1/requesthistory/analytics/overview", "/v1.0/api/request-history/analytics/overview", "/api/v1/analytics/overview", "/v1.0/api/analytics/overview"))
            {
                await RequirePermissionAsync(requestContext, "RequestHistory", OperationTypeEnum.Read, token).ConfigureAwait(false);
                await SendJsonAsync(context, AnalyticsOverview(requestContext)).ConfigureAwait(false);
                return;
            }

            if (method == "POST" && IsPath(path, "/api/v1/requesthistory/bulk", "/v1.0/api/request-history/bulk"))
            {
                await RequirePermissionAsync(requestContext, "RequestHistory", OperationTypeEnum.Delete, token).ConfigureAwait(false);
                int removed = DeleteRequestHistory(requestContext, ReadJsonObject(context));
                await SavePlatformAsync(token).ConfigureAwait(false);
                await SendJsonAsync(context, new { removed }).ConfigureAwait(false);
                return;
            }

            if (method == "GET" && IsPath(path, "/api/v1/settings", "/v1.0/api/settings"))
            {
                await RequirePermissionAsync(requestContext, "Admin", OperationTypeEnum.Admin, token).ConfigureAwait(false);
                await SendJsonAsync(context, new { settings = _Settings, settingsFile = _SettingsFile }).ConfigureAwait(false);
                return;
            }

            if (method == "POST" && IsPath(path, "/api/v1/settings", "/v1.0/api/settings"))
            {
                await RequirePermissionAsync(requestContext, "Admin", OperationTypeEnum.Admin, token).ConfigureAwait(false);
                XenoSettings? incoming = JsonUtil.Deserialize<XenoSettings>(context.Request.DataAsString ?? "{}");
                if (incoming == null) throw new ArgumentException("Settings payload is invalid.");
                ApplySettings(incoming);
                await SaveSettingsAsync(token).ConfigureAwait(false);
                await SendJsonAsync(context, new { settings = _Settings, settingsFile = _SettingsFile, restartRequired = true }).ConfigureAwait(false);
                return;
            }

            if (method == "GET" && IsPath(path, "/api/v1/backup", "/v1.0/api/backup"))
            {
                await RequirePermissionAsync(requestContext, "Admin", OperationTypeEnum.Admin, token).ConfigureAwait(false);
                await SendJsonAsync(context, RequirePlatform().ToState()).ConfigureAwait(false);
                return;
            }

            if (method == "POST" && IsPath(path, "/api/v1/restore", "/v1.0/api/restore"))
            {
                await RequirePermissionAsync(requestContext, "Admin", OperationTypeEnum.Admin, token).ConfigureAwait(false);
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
            Dictionary<string, object?> body = ReadJsonObject(context);
            string? model = body.TryGetValue("model", out object? modelValue) ? modelValue?.ToString() : null;
            RoutingDecision decision = await RequireRouting().RouteAsync(requestContext, model, false, token).ConfigureAwait(false);

            if (!decision.Success)
            {
                await RecordAndSaveHistoryAsync(requestContext, context.Request.Method.ToString().ToUpperInvariant(), path, decision, token).ConfigureAwait(false);
                await SendStatusJsonAsync(context, decision.HttpStatusCode, new { error = decision.OutcomeCode, message = decision.Message }).ConfigureAwait(false);
                return;
            }

            await RelayClientRequestAsync(context, requestContext, path, decision, body, false, stream, token).ConfigureAwait(false);
        }

        private async Task HandleEmbeddingAsync(HttpContextBase context, RequestContext requestContext, string path, CancellationToken token)
        {
            Dictionary<string, object?> body = ReadJsonObject(context);
            string? model = body.TryGetValue("model", out object? modelValue) ? modelValue?.ToString() : null;
            RoutingDecision decision = await RequireRouting().RouteAsync(requestContext, model, true, token).ConfigureAwait(false);

            if (!decision.Success)
            {
                await RecordAndSaveHistoryAsync(requestContext, context.Request.Method.ToString().ToUpperInvariant(), path, decision, token).ConfigureAwait(false);
                await SendStatusJsonAsync(context, decision.HttpStatusCode, new { error = decision.OutcomeCode, message = decision.Message }).ConfigureAwait(false);
                return;
            }

            await RelayClientRequestAsync(context, requestContext, path, decision, body, true, false, token).ConfigureAwait(false);
        }

        private async Task RelayClientRequestAsync(
            HttpContextBase context,
            RequestContext requestContext,
            string path,
            RoutingDecision decision,
            Dictionary<string, object?> body,
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
                await RecordAndSaveHistoryAsync(requestContext, context.Request.Method.ToString().ToUpperInvariant(), path, decision, token).ConfigureAwait(false);
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
                await RecordAndSaveHistoryAsync(requestContext, context.Request.Method.ToString().ToUpperInvariant(), path, decision, token).ConfigureAwait(false);
                await SendStatusJsonAsync(context, decision.HttpStatusCode, new { error = decision.OutcomeCode, message = decision.Message }).ConfigureAwait(false);
                return;
            }

            string requestId = requestContext.RequestId;
            TaskCompletionSource<string> completion = new TaskCompletionSource<string>(TaskCreationOptions.RunContinuationsAsynchronously);
            lock (_PendingResponsesLock)
            {
                _PendingClientResponses[requestId] = completion;
            }

            XenoClientModel? selectedModel = client.Models.FirstOrDefault(item => String.Equals(item.Name, decision.EffectiveModel, StringComparison.OrdinalIgnoreCase));
            client.InFlightRequests++;
            try
            {
                string relativePath = NormalizeOpenAiPath(path);
                string payload = JsonUtil.Serialize(new
                {
                    type = "server.request",
                    requestId,
                    tenantId = requestContext.TenantId,
                    clientId = client.Id,
                    clientName = client.Name,
                    provider = selectedModel?.Provider.ToString(),
                    source = selectedModel?.Source,
                    method = context.Request.Method.ToString().ToUpperInvariant(),
                    relativePath,
                    model = decision.EffectiveModel,
                    embeddings,
                    stream,
                    body = JsonUtil.Serialize(body)
                });

                await SendToClientAsync(connection, payload, token).ConfigureAwait(false);
                string responseJson = await WaitForClientResponseAsync(requestId, completion, token).ConfigureAwait(false);
                await SendClientResponseAsync(context, requestContext, path, decision, responseJson, token).ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
                decision.Success = false;
                decision.HttpStatusCode = 504;
                decision.OutcomeCode = "ClientTimeout";
                decision.Message = "The selected client did not respond before the timeout.";
                await RecordAndSaveHistoryAsync(requestContext, context.Request.Method.ToString().ToUpperInvariant(), path, decision, CancellationToken.None).ConfigureAwait(false);
                await SendStatusJsonAsync(context, decision.HttpStatusCode, new { error = decision.OutcomeCode, message = decision.Message }).ConfigureAwait(false);
            }
            finally
            {
                client.InFlightRequests = Math.Max(0, client.InFlightRequests - 1);
                lock (_PendingResponsesLock)
                {
                    _PendingClientResponses.Remove(requestId);
                }
                await SavePlatformAsync(CancellationToken.None).ConfigureAwait(false);
            }
        }

        private async Task SendClientResponseAsync(
            HttpContextBase context,
            RequestContext requestContext,
            string path,
            RoutingDecision decision,
            string responseJson,
            CancellationToken token)
        {
            using (JsonDocument document = JsonDocument.Parse(responseJson))
            {
                JsonElement root = document.RootElement;
                bool success = root.TryGetProperty("success", out JsonElement successElement) && successElement.ValueKind == JsonValueKind.True;
                int statusCode = 200;
                if (root.TryGetProperty("statusCode", out JsonElement statusElement) && statusElement.TryGetInt32(out int parsedStatusCode))
                {
                    statusCode = parsedStatusCode;
                }
                else if (!success)
                {
                    statusCode = 502;
                }

                decision.Success = success;
                decision.HttpStatusCode = statusCode;
                decision.OutcomeCode = success ? "Completed" : "ClientRequestFailed";
                decision.Message = success ? "Client completed the routed request." : TryGetString(root, "error") ?? "Client request failed.";
                await RecordAndSaveHistoryAsync(requestContext, context.Request.Method.ToString().ToUpperInvariant(), path, decision, token).ConfigureAwait(false);

                context.Response.StatusCode = statusCode;
                string contentType = TryGetString(root, "contentType") ?? "application/json";
                context.Response.ContentType = contentType;
                string body = TryGetString(root, "body") ?? "{}";
                await context.Response.Send(body, context.Token).ConfigureAwait(false);
            }
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

        private async Task<string> WaitForClientResponseAsync(string requestId, TaskCompletionSource<string> completion, CancellationToken token)
        {
            using (CancellationTokenSource timeout = CancellationTokenSource.CreateLinkedTokenSource(token))
            {
                timeout.CancelAfter(TimeSpan.FromMilliseconds(Math.Max(1000, _Settings.WebSocket.RequestTimeoutMs)));
                using (timeout.Token.Register(delegate { completion.TrySetCanceled(timeout.Token); }))
                {
                    return await completion.Task.ConfigureAwait(false);
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
                    catch (JsonException)
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

            using (JsonDocument document = JsonDocument.Parse(json))
            {
                JsonElement root = document.RootElement;
                string type = TryGetString(root, "type") ?? String.Empty;
                if (String.Equals(type, "client.register", StringComparison.OrdinalIgnoreCase))
                {
                    string clientId = RegisterClient(root, session, requestContext);
                    registeredClientCallback(clientId);
                    await SavePlatformAsync(token).ConfigureAwait(false);
                    return "server.registered";
                }

                if (String.Equals(type, "client.heartbeat", StringComparison.OrdinalIgnoreCase))
                {
                    UpdateClientHeartbeat(TryGetString(root, "clientId"), requestContext);
                    await SavePlatformAsync(token).ConfigureAwait(false);
                    return "server.heartbeat";
                }

                if (String.Equals(type, "client.response", StringComparison.OrdinalIgnoreCase))
                {
                    CompleteClientResponse(root, json);
                    return null;
                }
            }

            return "server.ack";
        }

        private string RegisterClient(JsonElement root, WebSocketSession session, RequestContext requestContext)
        {
            XenoPlatform platform = RequirePlatform();
            string tenantId = TryGetString(root, "tenantId") ?? requestContext.TenantId;
            if (!String.Equals(tenantId, requestContext.TenantId, StringComparison.OrdinalIgnoreCase))
            {
                throw new UnauthorizedAccessException("Client tenant does not match authenticated tenant.");
            }

            string clientId = TryGetString(root, "clientId") ?? IdGenerator.NewId("xcl_");
            string name = TryGetString(root, "name") ?? clientId;

            XenoClientRecord? client = platform.Clients.FirstOrDefault(item => item.Id == clientId);
            if (client != null && !String.Equals(client.TenantId, tenantId, StringComparison.OrdinalIgnoreCase))
            {
                throw new UnauthorizedAccessException("Client ID belongs to a different tenant.");
            }

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

            client.TenantId = tenantId;
            client.Name = name;
            client.Connected = true;
            client.LastHeartbeatUtc = DateTime.UtcNow;
            lock (_ClientConnectionsLock)
            {
                _ClientConnections[clientId] = new ClientConnection(clientId, tenantId, session);
            }

            if (root.TryGetProperty("inventory", out JsonElement inventory)) RegisterClientInventory(platform, client, inventory);

            return clientId;
        }

        private static void RegisterClientInventory(XenoPlatform platform, XenoClientRecord client, JsonElement inventory)
        {
            client.ServiceState = ClientServiceStateEnum.Normal;
            client.Models.Clear();

            if (inventory.TryGetProperty("models", out JsonElement directModels) && directModels.ValueKind == JsonValueKind.Array)
            {
                RegisterClientModels(platform, client, "client", ApiProviderTypeEnum.OpenAICompatible, directModels);
            }

            if (inventory.TryGetProperty("providers", out JsonElement providers) && providers.ValueKind == JsonValueKind.Array)
            {
                RegisterClientSources(platform, client, providers);
            }

            if (inventory.TryGetProperty("runners", out JsonElement runners) && runners.ValueKind == JsonValueKind.Array)
            {
                RegisterClientSources(platform, client, runners);
            }
        }

        private static void RegisterClientSources(XenoPlatform platform, XenoClientRecord client, JsonElement sources)
        {
            foreach (JsonElement source in sources.EnumerateArray())
            {
                string sourceName = TryGetString(source, "name") ?? "local";
                string providerName = TryGetString(source, "provider") ?? "Ollama";
                ApiProviderTypeEnum provider = Enum.TryParse(providerName, true, out ApiProviderTypeEnum parsedProvider) ? parsedProvider : ApiProviderTypeEnum.OpenAICompatible;
                if (source.TryGetProperty("models", out JsonElement sourceModels) && sourceModels.ValueKind == JsonValueKind.Array)
                {
                    RegisterClientModels(platform, client, sourceName, provider, sourceModels);
                }
            }
        }

        private static void RegisterClientModels(XenoPlatform platform, XenoClientRecord client, string sourceName, ApiProviderTypeEnum provider, JsonElement models)
        {
            foreach (JsonElement model in models.EnumerateArray())
            {
                string? modelName = TryGetString(model, "name");
                if (String.IsNullOrWhiteSpace(modelName)) continue;

                bool supportsCompletions = TryGetBoolean(model, "supportsCompletions", true);
                bool supportsEmbeddings = TryGetBoolean(model, "supportsEmbeddings", false);
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

        private void UpdateClientHeartbeat(string? clientId, RequestContext requestContext)
        {
            if (String.IsNullOrWhiteSpace(clientId)) return;
            XenoClientRecord? client = RequirePlatform().Clients.FirstOrDefault(item => item.Id == clientId && item.TenantId == requestContext.TenantId);
            if (client == null) return;
            client.Connected = true;
            client.LastHeartbeatUtc = DateTime.UtcNow;
        }

        private void CompleteClientResponse(JsonElement root, string json)
        {
            string? requestId = TryGetString(root, "requestId");
            if (String.IsNullOrWhiteSpace(requestId)) return;

            TaskCompletionSource<string>? completion = null;
            lock (_PendingResponsesLock)
            {
                if (_PendingClientResponses.TryGetValue(requestId, out completion))
                {
                    _PendingClientResponses.Remove(requestId);
                }
            }

            completion?.TrySetResult(json);
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

        private async Task RecordAndSaveHistoryAsync(RequestContext requestContext, string method, string path, RoutingDecision decision, CancellationToken token)
        {
            RequireRouting().RecordHistory(requestContext, path, method, decision);
            await SavePlatformAsync(token).ConfigureAwait(false);
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
            Dictionary<string, object?> body = ReadJsonObject(context);
            string accessKey = FirstNonEmpty(GetBodyString(body, "accessKey"), context.Request.Headers.Get("x-access-key"));
            string secretKey = FirstNonEmpty(GetBodyString(body, "secretKey"), context.Request.Headers.Get("x-secret-key"));

            XenoPlatform platform = RequirePlatform();
            Credential? credential = _Authentication.ValidateCredential(platform, requestContext.TenantId, accessKey, secretKey);
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
            Dictionary<string, object?> body = ReadJsonObject(context);
            string tenantId = ResolveTenantId(context, GetBodyString(body, "tenantId"));
            string username = FirstNonEmpty(GetBodyString(body, "username"), GetBodyString(body, "email"));
            string password = FirstNonEmpty(GetBodyString(body, "password"));
            if (String.IsNullOrWhiteSpace(username) || String.IsNullOrWhiteSpace(password))
            {
                throw new ArgumentException("Username and password are required.");
            }

            XenoPlatform platform = RequirePlatform();
            User? user = _Authentication.ValidateUserPassword(platform, tenantId, username, password);
            RequestContext loginContext = new RequestContext
            {
                TenantId = tenantId,
                SourceIp = context.Request.Source.IpAddress,
                PrincipalType = PrincipalTypeEnum.User,
                UserId = user?.Id,
                IsAuthenticated = user != null,
                AuthScheme = AuthSchemeEnum.Password
            };

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
                SecretKey = _Authentication.GenerateSecret(),
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
                secretKey = credential.SecretKey,
                authScheme = "Credential",
                warning = "SecretKey is shown once. The Xeno client stores this credential; it does not store the user's password."
            }).ConfigureAwait(false);
        }

        private async Task RequirePermissionAsync(RequestContext context, string resourceType, OperationTypeEnum operationType, CancellationToken token)
        {
            if (!_Settings.Rest.RequireAuthentication && context.AuthScheme == AuthSchemeEnum.None)
            {
                return;
            }

            if (!context.IsAuthenticated)
            {
                _Authentication.AppendAudit(RequirePlatform(), context, resourceType, operationType, false, "Unauthenticated", 401);
                await SavePlatformAsync(token).ConfigureAwait(false);
                throw new UnauthorizedAccessException("Authentication is required.");
            }

            if (!_Authentication.IsAllowed(RequirePlatform(), context, resourceType, operationType))
            {
                _Authentication.AppendAudit(RequirePlatform(), context, resourceType, operationType, false, "PermissionDenied", 403);
                await SavePlatformAsync(token).ConfigureAwait(false);
                throw new UnauthorizedAccessException("Permission denied.");
            }
        }

        private Credential CreateCredential(RequestContext requestContext, HttpContextBase context)
        {
            Dictionary<string, object?> body = ReadJsonObject(context);
            string userId = FirstNonEmpty(GetBodyString(body, "userId"), requestContext.UserId);
            if (String.IsNullOrWhiteSpace(userId))
            {
                User? user = RequirePlatform().Users.FirstOrDefault(item => item.TenantId == requestContext.TenantId && item.Active);
                userId = user?.Id ?? String.Empty;
            }

            string accessKey = FirstNonEmpty(GetBodyString(body, "accessKey"), IdGenerator.NewId("xeno_"));
            string secretKey = FirstNonEmpty(GetBodyString(body, "secretKey"), _Authentication.GenerateSecret());
            return new Credential
            {
                TenantId = requestContext.TenantId,
                UserId = userId,
                AccessKey = accessKey,
                SecretKey = secretKey,
                Active = true
            };
        }

        private int DeleteTenant(string tenantId)
        {
            XenoPlatform platform = RequirePlatform();
            List<string> clientIds = platform.Clients.Where(item => item.TenantId == tenantId).Select(item => item.Id).ToList();
            int removed = platform.Tenants.RemoveAll(item => item.Id == tenantId);
            if (removed == 0) return 0;

            removed += platform.Users.RemoveAll(item => item.TenantId == tenantId);
            removed += platform.Credentials.RemoveAll(item => item.TenantId == tenantId);
            removed += platform.AuthSessions.RemoveAll(item => item.TenantId == tenantId);
            removed += platform.Roles.RemoveAll(item => item.TenantId == tenantId);
            removed += platform.Permissions.RemoveAll(item => item.TenantId == tenantId);
            removed += platform.RoleAssignments.RemoveAll(item => item.TenantId == tenantId);
            removed += platform.AuditRecords.RemoveAll(item => item.TenantId == tenantId);
            removed += platform.Clients.RemoveAll(item => item.TenantId == tenantId);
            removed += platform.ModelDefinitions.RemoveAll(item => item.TenantId == tenantId);
            removed += platform.ModelConfigurations.RemoveAll(item => item.TenantId == tenantId);
            removed += platform.RequestHistory.RemoveAll(item => item.TenantId == tenantId);

            lock (_ClientConnectionsLock)
            {
                foreach (string clientId in clientIds) _ClientConnections.Remove(clientId);
            }

            return removed;
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
            return removed;
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

        private int DeleteRole(RequestContext requestContext, string roleId)
        {
            XenoPlatform platform = RequirePlatform();
            int removed = platform.Roles.RemoveAll(item => item.Id == roleId && item.TenantId == requestContext.TenantId);
            removed += platform.RoleAssignments.RemoveAll(item => item.RoleId == roleId && item.TenantId == requestContext.TenantId);
            return removed;
        }

        private int DeletePermission(RequestContext requestContext, string permissionId)
        {
            XenoPlatform platform = RequirePlatform();
            int removed = platform.Permissions.RemoveAll(item => item.Id == permissionId && item.TenantId == requestContext.TenantId);
            foreach (Role role in platform.Roles.Where(item => item.TenantId == requestContext.TenantId))
            {
                role.PermissionIds.RemoveAll(item => String.Equals(item, permissionId, StringComparison.OrdinalIgnoreCase));
            }

            return removed;
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
            Dictionary<string, object?> body = ReadJsonObject(context);
            string clientId = GetBodyString(body, "clientId") ?? String.Empty;
            string modelName = GetBodyString(body, "name") ?? String.Empty;
            string source = GetBodyString(body, "source") ?? String.Empty;
            if (String.IsNullOrWhiteSpace(clientId)) throw new ArgumentException("Client ID is required.");
            if (String.IsNullOrWhiteSpace(modelName)) throw new ArgumentException("Model name is required.");

            XenoClientRecord? client = RequirePlatform().Clients.FirstOrDefault(item => item.Id == clientId && item.TenantId == requestContext.TenantId);
            if (client == null) return 0;

            return client.Models.RemoveAll(item =>
                String.Equals(item.Name, modelName, StringComparison.OrdinalIgnoreCase)
                && (String.IsNullOrWhiteSpace(source) || String.Equals(item.Source, source, StringComparison.OrdinalIgnoreCase)));
        }

        private int DeleteRequestHistory(RequestContext requestContext, Dictionary<string, object?> body)
        {
            HashSet<string> ids = GetBodyStringSet(body, "ids");
            string? singleId = GetBodyString(body, "id");
            if (!String.IsNullOrWhiteSpace(singleId)) ids.Add(singleId);
            string? model = GetBodyString(body, "model");
            string? path = GetBodyString(body, "path");
            string? selectedClientId = GetBodyString(body, "selectedClientId");

            return RequirePlatform().RequestHistory.RemoveAll(item =>
                item.TenantId == requestContext.TenantId
                && (ids.Count == 0 || ids.Contains(item.Id))
                && MatchesOptionalFilter(item.Model, model)
                && MatchesOptionalFilter(item.Path, path)
                && MatchesOptionalFilter(item.SelectedClientId, selectedClientId));
        }

        private RequestContext CreateRequestContext(HttpContextBase context)
        {
            string tenantId = ResolveTenantId(context, null);

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

            RequestContext requestContext = new RequestContext
            {
                TenantId = tenantId,
                SourceIp = context.Request.Source.IpAddress,
                IsAuthenticated = false
            };

            if (!String.IsNullOrWhiteSpace(sessionToken))
            {
                AuthSession? session = _Authentication.ValidateToken(RequirePlatform(), tenantId, sessionToken);
                if (session == null) throw new UnauthorizedAccessException("Invalid or expired token.");
                requestContext.IsAuthenticated = true;
                requestContext.SessionId = session.Id;
                requestContext.UserId = session.UserId;
                requestContext.CredentialId = session.CredentialId;
                requestContext.PrincipalType = session.PrincipalType;
                requestContext.AuthScheme = AuthSchemeEnum.Bearer;
                return requestContext;
            }

            if (!String.IsNullOrWhiteSpace(accessKey) || !String.IsNullOrWhiteSpace(secretKey))
            {
                Credential? credential = _Authentication.ValidateCredential(RequirePlatform(), tenantId, accessKey, secretKey);
                if (credential == null) throw new UnauthorizedAccessException("Invalid credential.");
                requestContext.IsAuthenticated = true;
                requestContext.CredentialId = credential.Id;
                requestContext.UserId = credential.UserId;
                requestContext.PrincipalType = PrincipalTypeEnum.Credential;
                requestContext.AuthScheme = AuthSchemeEnum.Credential;
                return requestContext;
            }

            if (_Settings.Rest.RequireAuthentication)
            {
                throw new UnauthorizedAccessException("Authentication is required.");
            }

            User? devUser = RequirePlatform().Users.FirstOrDefault(item => item.TenantId == tenantId && item.Active && (item.IsAdmin || item.IsTenantAdmin));
            requestContext.IsAuthenticated = devUser != null;
            requestContext.UserId = devUser?.Id;
            requestContext.PrincipalType = devUser == null ? PrincipalTypeEnum.None : PrincipalTypeEnum.User;
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
                throw new UnauthorizedAccessException("WebSocket clients must authenticate with x-access-key/x-secret-key, not bearer tokens or user passwords.");
            }

            string accessKey = context.Request.Headers.Get("x-access-key") ?? String.Empty;
            string secretKey = context.Request.Headers.Get("x-secret-key") ?? String.Empty;
            RequestContext requestContext = new RequestContext
            {
                TenantId = tenantId,
                SourceIp = context.Request.Source.IpAddress,
                IsAuthenticated = false
            };

            if (!String.IsNullOrWhiteSpace(accessKey) || !String.IsNullOrWhiteSpace(secretKey))
            {
                Credential? credential = _Authentication.ValidateCredential(RequirePlatform(), tenantId, accessKey, secretKey);
                if (credential == null) throw new UnauthorizedAccessException("Invalid WebSocket credential.");
                requestContext.IsAuthenticated = true;
                requestContext.CredentialId = credential.Id;
                requestContext.UserId = credential.UserId;
                requestContext.PrincipalType = PrincipalTypeEnum.Credential;
                requestContext.AuthScheme = AuthSchemeEnum.Credential;
                return requestContext;
            }

            throw new UnauthorizedAccessException("WebSocket authentication requires a client access key and secret key.");
        }

        private static string ResolveTenantId(HttpContextBase context, string? bodyTenantId)
        {
            string? tenantHeader = context.Request.Headers.Get("x-tenant-guid");
            string? tenantHeaderAlternate = context.Request.Headers.Get("x-tenant-id");
            string? tenantQuery = context.Request.Query.Elements.Get("tenantId");
            string tenantId = FirstNonEmpty(tenantHeader, tenantHeaderAlternate, tenantQuery, bodyTenantId, Constants.DefaultTenantId);

            List<string> hints = new List<string>();
            AddNonEmpty(hints, tenantHeader);
            AddNonEmpty(hints, tenantHeaderAlternate);
            AddNonEmpty(hints, tenantQuery);
            AddNonEmpty(hints, bodyTenantId);
            if (hints.Distinct(StringComparer.OrdinalIgnoreCase).Count() > 1)
            {
                throw new ArgumentException("Conflicting tenant identifiers were supplied.");
            }

            return tenantId;
        }

        private static string? GetBodyString(Dictionary<string, object?> body, string key)
        {
            return body.TryGetValue(key, out object? value) ? value?.ToString() : null;
        }

        private static HashSet<string> GetBodyStringSet(Dictionary<string, object?> body, string key)
        {
            HashSet<string> values = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            if (!body.TryGetValue(key, out object? value) || value == null) return values;

            if (value is JsonElement element)
            {
                if (element.ValueKind == JsonValueKind.Array)
                {
                    foreach (JsonElement item in element.EnumerateArray())
                    {
                        string? text = item.ToString();
                        if (!String.IsNullOrWhiteSpace(text)) values.Add(text);
                    }
                    return values;
                }

                if (element.ValueKind == JsonValueKind.String)
                {
                    string? text = element.GetString();
                    if (!String.IsNullOrWhiteSpace(text)) values.Add(text);
                    return values;
                }
            }

            string? single = value.ToString();
            if (!String.IsNullOrWhiteSpace(single)) values.Add(single);
            return values;
        }

        private static bool MatchesOptionalFilter(string? value, string? filter)
        {
            if (String.IsNullOrWhiteSpace(filter)) return true;
            if (String.Equals(filter, "unknown", StringComparison.OrdinalIgnoreCase) && String.IsNullOrWhiteSpace(value)) return true;
            return String.Equals(value, filter, StringComparison.OrdinalIgnoreCase);
        }

        private static bool IsTenantVisible(RequestContext context, string tenantId)
        {
            return String.Equals(context.TenantId, Constants.DefaultTenantId, StringComparison.OrdinalIgnoreCase)
                || String.Equals(context.TenantId, tenantId, StringComparison.OrdinalIgnoreCase);
        }

        private static bool GetBodyBoolean(Dictionary<string, object?> body, string key)
        {
            if (!body.TryGetValue(key, out object? value)) return false;
            if (value == null) return false;
            return Boolean.TryParse(value.ToString(), out bool result) && result;
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
            string suffix = credential.SecretKey.Length > 4 ? credential.SecretKey.Substring(credential.SecretKey.Length - 4) : credential.SecretKey;
            return new
            {
                credential.Id,
                credential.TenantId,
                credential.UserId,
                credential.AccessKey,
                secretKey = "****" + suffix,
                credential.Active
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
                "xeno_request_failures_total " + failures + "\n";
            await context.Response.Send(metrics, context.Token).ConfigureAwait(false);
        }

        private object AnalyticsOverview(RequestContext requestContext)
        {
            List<RequestHistoryEntry> entries = FilterRequestHistory(requestContext);
            int total = entries.Count;
            int failures = entries.Count(item => item.StatusCode >= 400);
            List<object> byModel = entries
                .Where(item => item.RoutingDecision != null && !String.IsNullOrWhiteSpace(item.RoutingDecision.EffectiveModel))
                .GroupBy(item => item.RoutingDecision!.EffectiveModel)
                .Select(group => (object)new { model = group.Key, count = group.Count() })
                .ToList();
            List<object> byClient = entries
                .Where(item => !String.IsNullOrWhiteSpace(item.SelectedClientId))
                .GroupBy(item => item.SelectedClientId)
                .Select(group => (object)new { clientId = group.Key, count = group.Count() })
                .ToList();

            return new
            {
                total,
                failures,
                success = total - failures,
                failureRate = total == 0 ? 0 : (double)failures / total,
                byModel,
                byClient
            };
        }

        private List<RequestHistoryEntry> FilterRequestHistory(RequestContext context)
        {
            return RequirePlatform().RequestHistory
                .Where(item => item.TenantId == context.TenantId)
                .OrderByDescending(item => item.CreatedUtc)
                .ToList();
        }

        private object SummarizeRequestHistory(RequestContext requestContext, HttpContextBase context)
        {
            DateTime toUtc = DateTime.UtcNow;
            DateTime fromUtc = toUtc.AddHours(-24);
            int bucketMinutes = 60;

            string? fromValue = context.Request.Query.Elements.Get("fromUtc");
            string? toValue = context.Request.Query.Elements.Get("toUtc");
            string? bucketValue = context.Request.Query.Elements.Get("bucketMinutes");

            if (DateTime.TryParse(fromValue, out DateTime parsedFrom)) fromUtc = parsedFrom.ToUniversalTime();
            if (DateTime.TryParse(toValue, out DateTime parsedTo)) toUtc = parsedTo.ToUniversalTime();
            if (Int32.TryParse(bucketValue, out int parsedBucket) && parsedBucket > 0) bucketMinutes = parsedBucket;

            List<RequestHistoryEntry> entries = FilterRequestHistory(requestContext);
            List<object> buckets = new List<object>();
            DateTime cursor = fromUtc;
            while (cursor < toUtc)
            {
                DateTime bucketEnd = cursor.AddMinutes(bucketMinutes);
                int count = entries.Count(item => item.CreatedUtc >= cursor && item.CreatedUtc < bucketEnd);
                int errors = entries.Count(item => item.CreatedUtc >= cursor && item.CreatedUtc < bucketEnd && item.StatusCode >= 400);
                buckets.Add(new { startUtc = cursor, endUtc = bucketEnd, count, errors });
                cursor = bucketEnd;
            }

            return new { fromUtc, toUtc, bucketMinutes, buckets };
        }

        private static Dictionary<string, object?> ReadJsonObject(HttpContextBase context)
        {
            string json = context.Request.DataAsString ?? String.Empty;
            if (String.IsNullOrWhiteSpace(json)) return new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase);

            Dictionary<string, object?>? values = JsonSerializer.Deserialize<Dictionary<string, object?>>(json, JsonUtil.Options);
            return values ?? new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase);
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

        private void ApplyCors(HttpResponseBase response)
        {
            if (!_Settings.Cors.Enabled) return;
            response.Headers.Add("Access-Control-Allow-Origin", String.Join(",", _Settings.Cors.AllowedOrigins));
            response.Headers.Add("Access-Control-Allow-Headers", String.Join(",", _Settings.Cors.AllowedHeaders));
            response.Headers.Add("Access-Control-Allow-Methods", String.Join(",", _Settings.Cors.AllowedMethods));
            response.Headers.Add("Access-Control-Expose-Headers", "Content-Type, Authorization, x-token, x-tenant-guid");
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

        private static string FirstNonEmpty(params string?[] values)
        {
            foreach (string? value in values)
            {
                if (!String.IsNullOrWhiteSpace(value)) return value;
            }

            return String.Empty;
        }

        private static void AddNonEmpty(List<string> values, string? value)
        {
            if (!String.IsNullOrWhiteSpace(value)) values.Add(value);
        }

        private static string? TryGetString(JsonElement element, string propertyName)
        {
            if (!element.TryGetProperty(propertyName, out JsonElement property)) return null;
            if (property.ValueKind == JsonValueKind.Null || property.ValueKind == JsonValueKind.Undefined) return null;
            return property.ToString();
        }

        private static bool TryGetBoolean(JsonElement element, string propertyName, bool defaultValue)
        {
            if (!element.TryGetProperty(propertyName, out JsonElement property)) return defaultValue;
            if (property.ValueKind == JsonValueKind.True) return true;
            if (property.ValueKind == JsonValueKind.False) return false;
            if (Boolean.TryParse(property.ToString(), out bool parsed)) return parsed;
            return defaultValue;
        }

        private object BuildOpenApi()
        {
            return new
            {
                openapi = "3.0.1",
                info = new { title = "Xeno API", version = Constants.Version, description = "Xeno AI control plane API." },
                paths = new Dictionary<string, object>
                {
                    ["/health"] = Path(Operation("get", "health", "Health", "Health check", "Returns current server health.", "HealthResponse", false)),
                    ["/v1.0/api/auth/login"] = Path(Operation("post", "login", "Authentication", "Login", "Validates a user password and returns a one-time-visible client credential for WebSocket use.", "CredentialLoginResponse", false)),
                    ["/v1.0/api/auth/token"] = Path(Operation("post", "createToken", "Authentication", "Create token", "Creates a development session token.", "TokenResponse", false)),
                    ["/v1.0/api/tenants"] = Path(Operation("get", "listTenants", "Tenants", "List tenants", "Lists tenants visible to the caller.", "TenantArray", true)),
                    ["/v1.0/api/tenants/{id}"] = Path(Operation("delete", "deleteTenant", "Tenants", "Delete tenant", "Deletes a tenant and its scoped records.", "GenericObject", true)),
                    ["/v1.0/api/users"] = Path(Operation("get", "listUsers", "Tenants", "List users", "Lists tenant users.", "UserArray", true)),
                    ["/v1.0/api/users/{id}"] = Path(Operation("delete", "deleteUser", "Tenants", "Delete user", "Deletes a tenant user and related sessions or credentials.", "GenericObject", true)),
                    ["/v1.0/api/credentials"] = Path(Operation("get", "listCredentials", "Tenants", "List credentials", "Lists tenant credentials with redacted secrets.", "CredentialArray", true)),
                    ["/v1.0/api/credentials/{id}"] = Path(Operation("delete", "deleteCredential", "Tenants", "Delete credential", "Deletes a credential and related sessions.", "GenericObject", true)),
                    ["/v1.0/api/roles"] = Path(Operation("get", "listRoles", "Authorization", "List roles", "Lists tenant roles.", "RoleArray", true)),
                    ["/v1.0/api/roles/{id}"] = Path(Operation("delete", "deleteRole", "Authorization", "Delete role", "Deletes a tenant role and related assignments.", "GenericObject", true)),
                    ["/v1.0/api/permissions"] = Path(Operation("get", "listPermissions", "Authorization", "List permissions", "Lists tenant permissions.", "PermissionArray", true)),
                    ["/v1.0/api/permissions/{id}"] = Path(Operation("delete", "deletePermission", "Authorization", "Delete permission", "Deletes a tenant permission and removes it from roles.", "GenericObject", true)),
                    ["/v1.0/api/effective-permissions"] = Path(Operation("get", "listEffectivePermissions", "Authorization", "Effective permissions", "Lists effective permissions.", "EffectivePermissionArray", true)),
                    ["/v1.0/api/audit"] = Path(Operation("get", "listAudit", "Audit", "List audit records", "Lists tenant audit records.", "AuditRecordArray", true)),
                    ["/v1.0/api/audit/{id}"] = Path(Operation("delete", "deleteAuditRecord", "Audit", "Delete audit record", "Deletes one tenant audit record.", "GenericObject", true)),
                    ["/v1.0/api/clients"] = Path(Operation("get", "listClients", "Clients", "List clients", "Lists connected or registered Xeno clients and their advertised models.", "ClientArray", true)),
                    ["/v1.0/api/clients/{id}"] = Path(Operation("delete", "deleteClient", "Clients", "Delete client", "Deletes a registered Xeno client.", "GenericObject", true)),
                    ["/v1.0/api/client-models"] = Path(Operation("delete", "deleteClientModel", "Clients", "Delete client model", "Deletes one model advertised by a client.", "GenericObject", true)),
                    ["/v1.0/api/model-definitions"] = Path(Operation("get", "listModelDefinitions", "Routing", "List model definitions", "Lists model definitions available for routing.", "ModelDefinitionArray", true)),
                    ["/v1.0/api/model-configurations"] = Path(Operation("get", "listModelConfigurations", "Routing", "List model configurations", "Lists pinned model configurations.", "ModelConfigurationArray", true)),
                    ["/v1.0/api/request-history"] = Path(Operation("get", "listRequestHistory", "Request History", "List request history", "Lists captured request history entries.", "RequestHistoryArray", true)),
                    ["/v1.0/api/request-history/{id}"] = Path(Operation("get", "getRequestHistory", "Request History", "Get request history", "Returns a captured request history entry.", "RequestHistoryEntry", true), Operation("delete", "deleteRequestHistory", "Request History", "Delete request history", "Deletes one captured request history entry.", "GenericObject", true)),
                    ["/v1.0/api/request-history/summary"] = Path(Operation("get", "summarizeRequestHistory", "Request History", "Request history summary", "Returns bucketed request counts.", "RequestHistorySummary", true)),
                    ["/v1.0/api/request-history/bulk"] = Path(Operation("post", "bulkDeleteRequestHistory", "Request History", "Bulk delete request history", "Deletes tenant request history entries.", "GenericObject", true)),
                    ["/v1.0/api/request-history/analytics/overview"] = Path(Operation("get", "requestAnalyticsOverview", "Request History", "Analytics overview", "Returns tenant request analytics.", "GenericObject", true)),
                    ["/v1.0/api/analytics/overview"] = Path(Operation("get", "analyticsOverview", "Analytics", "Analytics overview", "Returns tenant analytics.", "GenericObject", true)),
                    ["/v1.0/api/settings"] = Path(Operation("get", "getSettings", "Settings", "Get server settings", "Returns running server settings and config file path.", "SettingsResponse", true), Operation("post", "updateSettings", "Settings", "Update server settings", "Persists server settings to the JSON config file.", "SettingsResponse", true)),
                    ["/v1.0/api/backup"] = Path(Operation("get", "backup", "Backup", "Backup", "Returns platform state backup.", "GenericObject", true)),
                    ["/v1.0/api/restore"] = Path(Operation("post", "restore", "Backup", "Restore", "Restores platform state backup.", "GenericObject", true)),
                    ["/v1/models"] = Path(Operation("get", "listOpenAiModels", "OpenAI", "List models", "OpenAI-compatible model list.", "OpenAiModelList", true)),
                    ["/v1/chat/completions"] = Path(Operation("post", "createChatCompletion", "OpenAI", "Create chat completion", "OpenAI-compatible chat completion.", "OpenAiChatCompletion", true)),
                    ["/v1/completions"] = Path(Operation("post", "createCompletion", "OpenAI", "Create completion", "OpenAI-compatible completion.", "OpenAiChatCompletion", true)),
                    ["/v1/embeddings"] = Path(Operation("post", "createEmbedding", "OpenAI", "Create embeddings", "OpenAI-compatible embeddings.", "OpenAiEmbeddingList", true))
                },
                components = new
                {
                    securitySchemes = new Dictionary<string, object>
                    {
                        ["bearerAuth"] = new { type = "http", scheme = "bearer", bearerFormat = "Xeno token" }
                    },
                    schemas = new Dictionary<string, object>
                    {
                        ["HealthResponse"] = ObjectSchema(Property("status", StringSchema()), Property("product", StringSchema()), Property("version", StringSchema())),
                        ["CredentialLoginResponse"] = ObjectSchema(Property("id", StringSchema()), Property("tenantId", StringSchema()), Property("userId", StringSchema()), Property("accessKey", StringSchema()), Property("secretKey", StringSchema()), Property("authScheme", StringSchema())),
                        ["TokenResponse"] = ObjectSchema(Property("token", StringSchema()), Property("tokenType", StringSchema()), Property("tenantId", StringSchema()), Property("expiresUtc", StringSchema("date-time"))),
                        ["TenantArray"] = ArraySchema("Tenant"),
                        ["UserArray"] = ArraySchema("User"),
                        ["CredentialArray"] = ArraySchema("Credential"),
                        ["RoleArray"] = ArraySchema("Role"),
                        ["PermissionArray"] = ArraySchema("Permission"),
                        ["EffectivePermissionArray"] = ArraySchema("EffectivePermission"),
                        ["AuditRecordArray"] = ArraySchema("AuditRecord"),
                        ["ClientArray"] = ArraySchema("XenoClientRecord"),
                        ["ModelDefinitionArray"] = ArraySchema("ModelDefinition"),
                        ["ModelConfigurationArray"] = ArraySchema("ModelConfiguration"),
                        ["RequestHistoryArray"] = ArraySchema("RequestHistoryEntry"),
                        ["RequestHistorySummary"] = ObjectSchema(Property("buckets", new Dictionary<string, object> { ["type"] = "array", ["items"] = ObjectSchema() })),
                        ["RoutingDecision"] = ObjectSchema(Property("success", BooleanSchema()), Property("outcomeCode", StringSchema()), Property("message", StringSchema())),
                        ["OpenAiModelList"] = ObjectSchema(Property("object", StringSchema()), Property("data", new Dictionary<string, object> { ["type"] = "array", ["items"] = ObjectSchema() })),
                        ["OpenAiChatCompletion"] = ObjectSchema(Property("id", StringSchema()), Property("object", StringSchema()), Property("model", StringSchema())),
                        ["OpenAiEmbeddingList"] = ObjectSchema(Property("object", StringSchema()), Property("model", StringSchema())),
                        ["SettingsResponse"] = ObjectSchema(Property("settings", ObjectSchema()), Property("settingsFile", StringSchema()), Property("restartRequired", BooleanSchema())),
                        ["GenericObject"] = ObjectSchema(),
                        ["Tenant"] = ObjectSchema(Property("id", StringSchema()), Property("name", StringSchema())),
                        ["User"] = ObjectSchema(Property("id", StringSchema()), Property("tenantId", StringSchema()), Property("email", StringSchema())),
                        ["Credential"] = ObjectSchema(Property("id", StringSchema()), Property("tenantId", StringSchema()), Property("accessKey", StringSchema())),
                        ["Role"] = ObjectSchema(Property("id", StringSchema()), Property("tenantId", StringSchema()), Property("name", StringSchema())),
                        ["Permission"] = ObjectSchema(Property("id", StringSchema()), Property("tenantId", StringSchema()), Property("resourceType", StringSchema())),
                        ["EffectivePermission"] = ObjectSchema(Property("resourceType", StringSchema()), Property("sourceRole", StringSchema())),
                        ["AuditRecord"] = ObjectSchema(Property("id", StringSchema()), Property("tenantId", StringSchema()), Property("reasonCode", StringSchema())),
                        ["XenoClientRecord"] = ObjectSchema(Property("id", StringSchema()), Property("tenantId", StringSchema()), Property("name", StringSchema()), Property("connected", BooleanSchema()), Property("models", new Dictionary<string, object> { ["type"] = "array", ["items"] = ObjectSchema() })),
                        ["ModelDefinition"] = ObjectSchema(Property("id", StringSchema()), Property("tenantId", StringSchema()), Property("name", StringSchema())),
                        ["ModelConfiguration"] = ObjectSchema(Property("id", StringSchema()), Property("tenantId", StringSchema()), Property("name", StringSchema())),
                        ["RequestHistoryEntry"] = ObjectSchema(Property("id", StringSchema()), Property("tenantId", StringSchema()), Property("path", StringSchema()), Property("statusCode", IntegerSchema()))
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
</head>
<body>
  <div id="swagger-ui"></div>
  <script src="https://unpkg.com/swagger-ui-dist@5/swagger-ui-bundle.js"></script>
  <script>
    window.onload = () => SwaggerUIBundle({
      url: '/openapi.json',
      dom_id: '#swagger-ui',
      deepLinking: true,
      displayRequestDuration: true,
      persistAuthorization: true,
      tryItOutEnabled: true
    });
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

            if (auth) operation["security"] = new object[] { new Dictionary<string, object> { ["bearerAuth"] = Array.Empty<string>() } };
            return new KeyValuePair<string, object>(method, operation);
        }

        private static Dictionary<string, object> ObjectSchema(params KeyValuePair<string, object>[] properties)
        {
            Dictionary<string, object> propertyMap = new Dictionary<string, object>();
            foreach (KeyValuePair<string, object> property in properties) propertyMap[property.Key] = property.Value;
            return new Dictionary<string, object> { ["type"] = "object", ["properties"] = propertyMap };
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

        private static Dictionary<string, object> IntegerSchema()
        {
            return new Dictionary<string, object> { ["type"] = "integer", ["format"] = "int32" };
        }
    }
}
