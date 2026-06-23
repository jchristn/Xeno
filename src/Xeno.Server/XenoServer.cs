namespace Xeno.Server
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Net.WebSockets;
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
        private readonly Webserver _Webserver;
        private DatabaseDriverBase? _Database;
        private RoutingService? _Routing;
        private bool _Disposed;

        /// <summary>
        /// Instantiate the server.
        /// </summary>
        /// <param name="settings">Server settings.</param>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="settings"/> is null.</exception>
        public XenoServer(XenoSettings settings)
        {
            _Settings = settings ?? throw new ArgumentNullException(nameof(settings));

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
            _Webserver.Stop();
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
                openApi.Tags.Add(new OpenApiTag { Name = "Clients", Description = "Connected Xeno clients" });
                openApi.Tags.Add(new OpenApiTag { Name = "Routing", Description = "Conductor-compatible routing controls" });
                openApi.Tags.Add(new OpenApiTag { Name = "OpenAI", Description = "OpenAI-compatible inference API" });
                openApi.Tags.Add(new OpenApiTag { Name = "Request History", Description = "Captured request history and summaries" });
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
            RegisterStaticRoute(HttpMethod.GET, "/v1.0/api/health", "Health check", "Health");
            RegisterStaticRoute(HttpMethod.POST, "/api/v1/auth/token", "Create token", "Authentication");
            RegisterStaticRoute(HttpMethod.POST, "/v1.0/api/auth/token", "Create token", "Authentication");
            RegisterStaticRoute(HttpMethod.GET, "/api/v1/tenants", "List tenants", "Tenants");
            RegisterStaticRoute(HttpMethod.GET, "/v1.0/api/tenants", "List tenants", "Tenants");
            RegisterStaticRoute(HttpMethod.GET, "/api/v1/users", "List users", "Tenants");
            RegisterStaticRoute(HttpMethod.GET, "/v1.0/api/users", "List users", "Tenants");
            RegisterStaticRoute(HttpMethod.GET, "/api/v1/credentials", "List credentials", "Tenants");
            RegisterStaticRoute(HttpMethod.GET, "/v1.0/api/credentials", "List credentials", "Tenants");
            RegisterStaticRoute(HttpMethod.GET, "/api/v1/clients", "List clients", "Clients");
            RegisterStaticRoute(HttpMethod.GET, "/v1.0/api/clients", "List clients", "Clients");
            RegisterStaticRoute(HttpMethod.GET, "/api/v1/modelrunnerendpoints", "List model runner endpoints", "Routing");
            RegisterStaticRoute(HttpMethod.GET, "/v1.0/api/model-runner-endpoints", "List model runner endpoints", "Routing");
            RegisterStaticRoute(HttpMethod.GET, "/api/v1/modeldefinitions", "List model definitions", "Routing");
            RegisterStaticRoute(HttpMethod.GET, "/v1.0/api/model-definitions", "List model definitions", "Routing");
            RegisterStaticRoute(HttpMethod.GET, "/api/v1/modelconfigurations", "List model configurations", "Routing");
            RegisterStaticRoute(HttpMethod.GET, "/v1.0/api/model-configurations", "List model configurations", "Routing");
            RegisterStaticRoute(HttpMethod.GET, "/api/v1/loadbalancingpolicies", "List load-balancing policies", "Routing");
            RegisterStaticRoute(HttpMethod.GET, "/v1.0/api/load-balancing-policies", "List load-balancing policies", "Routing");
            RegisterStaticRoute(HttpMethod.GET, "/api/v1/modelaccesspolicies", "List model access policies", "Routing");
            RegisterStaticRoute(HttpMethod.GET, "/v1.0/api/model-access-policies", "List model access policies", "Routing");
            RegisterStaticRoute(HttpMethod.GET, "/api/v1/virtualmodelrunners", "List virtual model runners", "Routing");
            RegisterStaticRoute(HttpMethod.GET, "/v1.0/api/virtual-model-runners", "List virtual model runners", "Routing");
            RegisterStaticRoute(HttpMethod.GET, "/api/v1/virtualmodelrunnerreservations", "List virtual model runner reservations", "Routing");
            RegisterStaticRoute(HttpMethod.GET, "/v1.0/api/virtual-model-runner-reservations", "List virtual model runner reservations", "Routing");
            RegisterStaticRoute(HttpMethod.POST, "/v1.0/api/virtual-model-runners/explain-routing", "Explain routing", "Routing");
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
            _Webserver.Routes.PreAuthentication.Parameter.Add(
                HttpMethod.DELETE,
                "/v1.0/api/request-history/{id}",
                DefaultRouteAsync,
                ExceptionRouteAsync,
                openApiMetadata: OpenApiRouteMetadata.Create("Delete request history entry", "Request History"));
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

            RequestContext requestContext = CreateRequestContext(context);

            if (method == "POST" && IsPath(path, "/api/v1/auth/token", "/v1.0/api/auth/token"))
            {
                await SendJsonAsync(context, new { token = "dev-token", tokenType = "Bearer", tenantId = requestContext.TenantId, expiresUtc = DateTime.UtcNow.AddHours(1) }).ConfigureAwait(false);
                return;
            }

            if (method == "GET" && IsPath(path, "/api/v1/tenants", "/v1.0/api/tenants"))
            {
                await SendJsonAsync(context, RequirePlatform().Tenants).ConfigureAwait(false);
                return;
            }

            if (method == "GET" && IsPath(path, "/api/v1/users", "/v1.0/api/users"))
            {
                await SendJsonAsync(context, RequirePlatform().Users.Where(item => item.TenantId == requestContext.TenantId || requestContext.TenantId == Constants.DefaultTenantId).ToList()).ConfigureAwait(false);
                return;
            }

            if (method == "GET" && IsPath(path, "/api/v1/credentials", "/v1.0/api/credentials"))
            {
                await SendJsonAsync(context, RequirePlatform().Credentials.Where(item => item.TenantId == requestContext.TenantId).Select(RedactCredential).ToList()).ConfigureAwait(false);
                return;
            }

            if (method == "GET" && IsPath(path, "/api/v1/clients", "/v1.0/api/clients"))
            {
                await SendJsonAsync(context, RequirePlatform().Clients.Where(item => item.TenantId == requestContext.TenantId).ToList()).ConfigureAwait(false);
                return;
            }

            if (method == "GET" && IsPath(path, "/api/v1/modelrunnerendpoints", "/v1.0/api/model-runner-endpoints"))
            {
                await SendJsonAsync(context, RequirePlatform().Endpoints.Where(item => item.TenantId == requestContext.TenantId).ToList()).ConfigureAwait(false);
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

            if (method == "GET" && IsPath(path, "/api/v1/loadbalancingpolicies", "/v1.0/api/load-balancing-policies"))
            {
                await SendJsonAsync(context, RequirePlatform().LoadBalancingPolicies.Where(item => item.TenantId == requestContext.TenantId).ToList()).ConfigureAwait(false);
                return;
            }

            if (method == "GET" && IsPath(path, "/api/v1/modelaccesspolicies", "/v1.0/api/model-access-policies"))
            {
                await SendJsonAsync(context, RequirePlatform().ModelAccessPolicies.Where(item => item.TenantId == requestContext.TenantId).ToList()).ConfigureAwait(false);
                return;
            }

            if (method == "GET" && IsPath(path, "/api/v1/virtualmodelrunners", "/v1.0/api/virtual-model-runners"))
            {
                await SendJsonAsync(context, RequirePlatform().VirtualModelRunners.Where(item => item.TenantId == requestContext.TenantId).ToList()).ConfigureAwait(false);
                return;
            }

            if (method == "GET" && IsPath(path, "/api/v1/virtualmodelrunnerreservations", "/v1.0/api/virtual-model-runner-reservations"))
            {
                await SendJsonAsync(context, RequirePlatform().VirtualModelRunnerReservations.Where(item => item.TenantId == requestContext.TenantId).ToList()).ConfigureAwait(false);
                return;
            }

            if (method == "POST" && path.EndsWith("/explain-routing", StringComparison.OrdinalIgnoreCase))
            {
                Dictionary<string, object?> body = ReadJsonObject(context);
                string? model = body.TryGetValue("model", out object? modelValue) ? modelValue?.ToString() : null;
                bool embeddings = body.TryGetValue("embeddings", out object? embeddingsValue) && String.Equals(embeddingsValue?.ToString(), "true", StringComparison.OrdinalIgnoreCase);
                RoutingDecision decision = await RequireRouting().RouteAsync(requestContext, model, embeddings, token).ConfigureAwait(false);
                await SendStatusJsonAsync(context, decision.HttpStatusCode, decision).ConfigureAwait(false);
                return;
            }

            if (method == "GET" && IsPath(path, "/v1/models", "/api/v1/openai/v1/models"))
            {
                List<object> models = RequirePlatform().ModelDefinitions
                    .Where(item => item.TenantId == requestContext.TenantId)
                    .Select(item => (object)new { id = item.Name, @object = "model", owned_by = "xeno" })
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

            if (method == "GET" && path.Equals("/v1.0/api/request-history/summary", StringComparison.OrdinalIgnoreCase))
            {
                await SendJsonAsync(context, SummarizeRequestHistory(requestContext, context)).ConfigureAwait(false);
                return;
            }

            if (method == "DELETE" && path.StartsWith("/v1.0/api/request-history/", StringComparison.OrdinalIgnoreCase))
            {
                string id = path.Substring("/v1.0/api/request-history/".Length);
                RequestHistoryEntry? entry = RequirePlatform().RequestHistory.FirstOrDefault(item => item.Id == id && item.TenantId == requestContext.TenantId);
                if (entry != null) RequirePlatform().RequestHistory.Remove(entry);
                context.Response.StatusCode = 204;
                await context.Response.Send(context.Token).ConfigureAwait(false);
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
            RequireRouting().RecordHistory(requestContext, path, decision);

            if (!decision.Success)
            {
                await SendStatusJsonAsync(context, decision.HttpStatusCode, new { error = decision.OutcomeCode, message = decision.Message }).ConfigureAwait(false);
                return;
            }

            await SendJsonAsync(context, new
            {
                id = "chatcmpl_" + Guid.NewGuid().ToString("N"),
                @object = "chat.completion",
                created = DateTimeOffset.UtcNow.ToUnixTimeSeconds(),
                model = decision.EffectiveModel,
                choices = new[]
                {
                    new
                    {
                        index = 0,
                        message = new { role = "assistant", content = "Xeno routed this request to " + decision.SelectedEndpointId + "." },
                        finish_reason = "stop"
                    }
                },
                xeno = new { decision.SelectedClientId, decision.SelectedEndpointId, routed = true, stream }
            }).ConfigureAwait(false);
        }

        private async Task HandleEmbeddingAsync(HttpContextBase context, RequestContext requestContext, string path, CancellationToken token)
        {
            Dictionary<string, object?> body = ReadJsonObject(context);
            string? model = body.TryGetValue("model", out object? modelValue) ? modelValue?.ToString() : null;
            RoutingDecision decision = await RequireRouting().RouteAsync(requestContext, model, true, token).ConfigureAwait(false);
            RequireRouting().RecordHistory(requestContext, path, decision);

            if (!decision.Success)
            {
                await SendStatusJsonAsync(context, decision.HttpStatusCode, new { error = decision.OutcomeCode, message = decision.Message }).ConfigureAwait(false);
                return;
            }

            await SendJsonAsync(context, new
            {
                @object = "list",
                model = decision.EffectiveModel,
                data = new[]
                {
                    new { @object = "embedding", index = 0, embedding = new[] { 0.01, 0.02, 0.03 } }
                },
                xeno = new { decision.SelectedClientId, decision.SelectedEndpointId, routed = true }
            }).ConfigureAwait(false);
        }

        private async Task WebSocketRouteAsync(HttpContextBase context, WebSocketSession session)
        {
            await session.SendTextAsync(JsonUtil.Serialize(new
            {
                type = "server.connected",
                sessionId = session.Id.ToString(),
                heartbeatIntervalMs = _Settings.WebSocket.HeartbeatIntervalMs
            }), context.Token).ConfigureAwait(false);

            while (session.IsConnected && !context.Token.IsCancellationRequested)
            {
                WebSocketMessage message = await session.ReceiveAsync(context.Token).ConfigureAwait(false);
                if (message.MessageType == WebSocketMessageType.Close) break;

                string text = message.Text ?? String.Empty;
                string responseType;
                try
                {
                    responseType = HandleClientMessage(text);
                }
                catch (JsonException)
                {
                    responseType = "server.invalid-json";
                }
                await session.SendTextAsync(JsonUtil.Serialize(new
                {
                    type = responseType,
                    receivedUtc = DateTime.UtcNow,
                    heartbeatIntervalMs = _Settings.WebSocket.HeartbeatIntervalMs
                }), context.Token).ConfigureAwait(false);
            }

            await session.CloseAsync(WebSocketCloseStatus.NormalClosure, "closing", context.Token).ConfigureAwait(false);
        }

        private string HandleClientMessage(string json)
        {
            if (String.IsNullOrWhiteSpace(json)) return "server.empty";

            using (JsonDocument document = JsonDocument.Parse(json))
            {
                JsonElement root = document.RootElement;
                string type = TryGetString(root, "type") ?? String.Empty;
                if (String.Equals(type, "client.register", StringComparison.OrdinalIgnoreCase))
                {
                    RegisterClient(root);
                    return "server.registered";
                }

                if (String.Equals(type, "client.heartbeat", StringComparison.OrdinalIgnoreCase))
                {
                    UpdateClientHeartbeat(TryGetString(root, "clientId"));
                    return "server.heartbeat";
                }
            }

            return "server.ack";
        }

        private void RegisterClient(JsonElement root)
        {
            XenoPlatform platform = RequirePlatform();
            string tenantId = TryGetString(root, "tenantId") ?? Constants.DefaultTenantId;
            string clientId = TryGetString(root, "clientId") ?? "xcl_" + Guid.NewGuid().ToString("N");
            string name = TryGetString(root, "name") ?? clientId;

            XenoClientRecord? client = platform.Clients.FirstOrDefault(item => item.Id == clientId);
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

            if (!root.TryGetProperty("inventory", out JsonElement inventory)) return;
            if (!inventory.TryGetProperty("runners", out JsonElement runners) || runners.ValueKind != JsonValueKind.Array) return;

            foreach (JsonElement runner in runners.EnumerateArray())
            {
                RegisterRunner(platform, client, runner);
            }
        }

        private static void RegisterRunner(XenoPlatform platform, XenoClientRecord client, JsonElement runner)
        {
            string runnerName = TryGetString(runner, "name") ?? "client-runner";
            string endpointUrl = TryGetString(runner, "endpoint") ?? "http://127.0.0.1:11434";
            string providerName = TryGetString(runner, "provider") ?? "Ollama";
            ApiProviderTypeEnum provider = Enum.TryParse(providerName, true, out ApiProviderTypeEnum parsedProvider) ? parsedProvider : ApiProviderTypeEnum.OpenAICompatible;

            ModelRunnerEndpoint? endpoint = platform.Endpoints.FirstOrDefault(item =>
                item.ClientId == client.Id &&
                String.Equals(item.Name, runnerName, StringComparison.OrdinalIgnoreCase));

            if (endpoint == null)
            {
                endpoint = new ModelRunnerEndpoint
                {
                    TenantId = client.TenantId,
                    ClientId = client.Id,
                    Name = runnerName
                };
                platform.Endpoints.Add(endpoint);
            }

            endpoint.TenantId = client.TenantId;
            endpoint.ClientId = client.Id;
            endpoint.Name = runnerName;
            endpoint.EndpointUrl = endpointUrl;
            endpoint.Provider = provider;
            endpoint.Active = true;
            endpoint.ServiceState = EndpointServiceStateEnum.Normal;
            endpoint.Models.Clear();

            if (!runner.TryGetProperty("models", out JsonElement models) || models.ValueKind != JsonValueKind.Array) return;

            foreach (JsonElement model in models.EnumerateArray())
            {
                string? modelName = TryGetString(model, "name");
                if (String.IsNullOrWhiteSpace(modelName)) continue;

                endpoint.Models.Add(modelName);
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

                definition.SupportsCompletions = TryGetBoolean(model, "supportsCompletions", definition.SupportsCompletions);
                definition.SupportsEmbeddings = TryGetBoolean(model, "supportsEmbeddings", definition.SupportsEmbeddings);

                foreach (VirtualModelRunner vmr in platform.VirtualModelRunners.Where(item => item.TenantId == client.TenantId))
                {
                    if (!vmr.ModelRunnerEndpointIds.Contains(endpoint.Id)) vmr.ModelRunnerEndpointIds.Add(endpoint.Id);
                    if (!vmr.ModelDefinitionIds.Contains(definition.Id)) vmr.ModelDefinitionIds.Add(definition.Id);
                }
            }
        }

        private void UpdateClientHeartbeat(string? clientId)
        {
            if (String.IsNullOrWhiteSpace(clientId)) return;
            XenoClientRecord? client = RequirePlatform().Clients.FirstOrDefault(item => item.Id == clientId);
            if (client == null) return;
            client.Connected = true;
            client.LastHeartbeatUtc = DateTime.UtcNow;
        }

        private RequestContext CreateRequestContext(HttpContextBase context)
        {
            string? tenantHeader = context.Request.Headers.Get("x-tenant-guid");
            string? tenantHeaderAlternate = context.Request.Headers.Get("x-tenant-id");
            string? tenantQuery = context.Request.Query.Elements.Get("tenantId");
            string tenantId = FirstNonEmpty(tenantHeader, tenantHeaderAlternate, tenantQuery, Constants.DefaultTenantId);

            List<string> hints = new List<string>();
            AddNonEmpty(hints, tenantHeader);
            AddNonEmpty(hints, tenantHeaderAlternate);
            AddNonEmpty(hints, tenantQuery);
            if (hints.Distinct(StringComparer.OrdinalIgnoreCase).Count() > 1)
            {
                throw new ArgumentException("Conflicting tenant identifiers were supplied.");
            }

            string authorization = context.Request.Headers.Get("Authorization") ?? String.Empty;
            string tokenHeader = context.Request.Headers.Get("x-token") ?? String.Empty;
            string bearerToken = authorization.StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase) ? authorization.Substring(7).Trim() : String.Empty;
            if (!String.IsNullOrWhiteSpace(bearerToken) && !String.IsNullOrWhiteSpace(tokenHeader) && !String.Equals(bearerToken, tokenHeader, StringComparison.Ordinal))
            {
                throw new UnauthorizedAccessException("Authorization bearer token and x-token do not match.");
            }

            return new RequestContext
            {
                TenantId = tenantId,
                SourceIp = context.Request.Source.IpAddress,
                IsAuthenticated = true
            };
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
                    ["/v1.0/api/auth/token"] = Path(Operation("post", "createToken", "Authentication", "Create token", "Creates a development session token.", "TokenResponse", false)),
                    ["/v1.0/api/tenants"] = Path(Operation("get", "listTenants", "Tenants", "List tenants", "Lists tenants visible to the caller.", "TenantArray", true)),
                    ["/v1.0/api/users"] = Path(Operation("get", "listUsers", "Tenants", "List users", "Lists tenant users.", "UserArray", true)),
                    ["/v1.0/api/credentials"] = Path(Operation("get", "listCredentials", "Tenants", "List credentials", "Lists tenant credentials with redacted secrets.", "CredentialArray", true)),
                    ["/v1.0/api/clients"] = Path(Operation("get", "listClients", "Clients", "List clients", "Lists connected or registered Xeno clients.", "ClientArray", true)),
                    ["/v1.0/api/model-runner-endpoints"] = Path(Operation("get", "listModelRunnerEndpoints", "Routing", "List model runner endpoints", "Lists model runner endpoints advertised by clients.", "EndpointArray", true)),
                    ["/v1.0/api/model-definitions"] = Path(Operation("get", "listModelDefinitions", "Routing", "List model definitions", "Lists model definitions available for routing.", "ModelDefinitionArray", true)),
                    ["/v1.0/api/model-configurations"] = Path(Operation("get", "listModelConfigurations", "Routing", "List model configurations", "Lists pinned model configurations.", "ModelConfigurationArray", true)),
                    ["/v1.0/api/load-balancing-policies"] = Path(Operation("get", "listLoadBalancingPolicies", "Routing", "List load-balancing policies", "Lists load-balancing policies.", "LoadBalancingPolicyArray", true)),
                    ["/v1.0/api/model-access-policies"] = Path(Operation("get", "listModelAccessPolicies", "Routing", "List model access policies", "Lists model access policies.", "ModelAccessPolicyArray", true)),
                    ["/v1.0/api/virtual-model-runners"] = Path(Operation("get", "listVirtualModelRunners", "Routing", "List virtual model runners", "Lists virtual model runners.", "VirtualModelRunnerArray", true)),
                    ["/v1.0/api/virtual-model-runner-reservations"] = Path(Operation("get", "listVirtualModelRunnerReservations", "Routing", "List virtual model runner reservations", "Lists virtual model runner reservations.", "VirtualModelRunnerReservationArray", true)),
                    ["/v1.0/api/virtual-model-runners/explain-routing"] = Path(Operation("post", "explainRouting", "Routing", "Explain routing", "Explains the routing decision for a model request.", "RoutingDecision", true)),
                    ["/v1.0/api/request-history"] = Path(Operation("get", "listRequestHistory", "Request History", "List request history", "Lists captured request history entries.", "RequestHistoryArray", true)),
                    ["/v1.0/api/request-history/summary"] = Path(Operation("get", "summarizeRequestHistory", "Request History", "Request history summary", "Returns bucketed request counts.", "RequestHistorySummary", true)),
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
                        ["TokenResponse"] = ObjectSchema(Property("token", StringSchema()), Property("tokenType", StringSchema()), Property("tenantId", StringSchema()), Property("expiresUtc", StringSchema("date-time"))),
                        ["TenantArray"] = ArraySchema("Tenant"),
                        ["UserArray"] = ArraySchema("User"),
                        ["CredentialArray"] = ArraySchema("Credential"),
                        ["ClientArray"] = ArraySchema("XenoClientRecord"),
                        ["EndpointArray"] = ArraySchema("ModelRunnerEndpoint"),
                        ["ModelDefinitionArray"] = ArraySchema("ModelDefinition"),
                        ["ModelConfigurationArray"] = ArraySchema("ModelConfiguration"),
                        ["LoadBalancingPolicyArray"] = ArraySchema("LoadBalancingPolicy"),
                        ["ModelAccessPolicyArray"] = ArraySchema("ModelAccessPolicy"),
                        ["VirtualModelRunnerArray"] = ArraySchema("VirtualModelRunner"),
                        ["VirtualModelRunnerReservationArray"] = ArraySchema("VirtualModelRunnerReservation"),
                        ["RequestHistoryArray"] = ArraySchema("RequestHistoryEntry"),
                        ["RequestHistorySummary"] = ObjectSchema(Property("buckets", new Dictionary<string, object> { ["type"] = "array", ["items"] = ObjectSchema() })),
                        ["RoutingDecision"] = ObjectSchema(Property("success", BooleanSchema()), Property("outcomeCode", StringSchema()), Property("message", StringSchema())),
                        ["OpenAiModelList"] = ObjectSchema(Property("object", StringSchema()), Property("data", new Dictionary<string, object> { ["type"] = "array", ["items"] = ObjectSchema() })),
                        ["OpenAiChatCompletion"] = ObjectSchema(Property("id", StringSchema()), Property("object", StringSchema()), Property("model", StringSchema())),
                        ["OpenAiEmbeddingList"] = ObjectSchema(Property("object", StringSchema()), Property("model", StringSchema())),
                        ["Tenant"] = ObjectSchema(Property("id", StringSchema()), Property("name", StringSchema())),
                        ["User"] = ObjectSchema(Property("id", StringSchema()), Property("tenantId", StringSchema()), Property("email", StringSchema())),
                        ["Credential"] = ObjectSchema(Property("id", StringSchema()), Property("tenantId", StringSchema()), Property("accessKey", StringSchema())),
                        ["XenoClientRecord"] = ObjectSchema(Property("id", StringSchema()), Property("tenantId", StringSchema()), Property("name", StringSchema())),
                        ["ModelRunnerEndpoint"] = ObjectSchema(Property("id", StringSchema()), Property("tenantId", StringSchema()), Property("name", StringSchema())),
                        ["ModelDefinition"] = ObjectSchema(Property("id", StringSchema()), Property("tenantId", StringSchema()), Property("name", StringSchema())),
                        ["ModelConfiguration"] = ObjectSchema(Property("id", StringSchema()), Property("tenantId", StringSchema()), Property("name", StringSchema())),
                        ["LoadBalancingPolicy"] = ObjectSchema(Property("id", StringSchema()), Property("tenantId", StringSchema()), Property("name", StringSchema())),
                        ["ModelAccessPolicy"] = ObjectSchema(Property("id", StringSchema()), Property("tenantId", StringSchema()), Property("name", StringSchema())),
                        ["VirtualModelRunner"] = ObjectSchema(Property("id", StringSchema()), Property("tenantId", StringSchema()), Property("name", StringSchema())),
                        ["VirtualModelRunnerReservation"] = ObjectSchema(Property("id", StringSchema()), Property("tenantId", StringSchema()), Property("name", StringSchema())),
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
