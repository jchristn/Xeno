namespace Xeno.Sdk.Tests
{
    using System;
    using System.Collections.Generic;
    using System.Net;
    using System.Net.Http;
    using System.Text;
    using System.Threading;
    using System.Threading.Tasks;
    using Xunit;

    public sealed class XenoClientTests
    {
        [Fact]
        public async Task ListTenantsAsync_SendsTenantAndBearerHeaders()
        {
            RecordingHandler handler = new RecordingHandler(_ => JsonResponse("[]"));
            using (HttpClient httpClient = new HttpClient(handler))
            using (XenoClient client = new XenoClient("https://xeno.local", "token-123", "ten_123", httpClient))
            {
                List<Tenant> response = await client.ListTenantsAsync();
                Assert.Empty(response);

                RecordedRequest request = Assert.Single(handler.Requests);
                Assert.Equal(HttpMethod.Get, request.Method);
                Assert.Equal("https://xeno.local/v1.0/api/tenants", request.Uri?.ToString());
                Assert.Equal("Bearer", request.AuthorizationScheme);
                Assert.Equal("token-123", request.AuthorizationParameter);
                Assert.Equal("ten_123", request.TenantId);
            }
        }

        [Fact]
        public async Task ManagementCrudMethods_UseExpectedRoutesAndBodies()
        {
            RecordingHandler handler = new RecordingHandler(request =>
            {
                if (request.Method == HttpMethod.Delete) return new HttpResponseMessage(HttpStatusCode.NoContent);
                if (request.Method == HttpMethod.Get) return JsonResponse("[]");
                return JsonResponse("{}");
            });
            using (HttpClient httpClient = new HttpClient(handler))
            using (XenoClient client = new XenoClient("https://xeno.local", httpClient: httpClient))
            {
                await client.CreateTenantAsync(new { name = "Tenant" });
                await client.UpdateTenantAsync("ten_1", new { name = "Tenant 2" });
                await client.DeleteTenantAsync("ten_1");
                await client.CreateUserAsync(new { username = "user@xeno", password = "password" });
                await client.UpdateUserAsync("usr_1", new { active = false });
                await client.DeleteUserAsync("usr_1");
                await client.CreateCredentialAsync(new { userId = "usr_1" });
                await client.UpdateCredentialAsync("cred_1", new { active = true });
                await client.DeleteCredentialAsync("cred_1");
                await client.CreateRoleAsync(new { name = "Operator" });
                await client.UpdateRoleAsync("role_1", new { permissionIds = new[] { "perm_1" } });
                await client.DeleteRoleAsync("role_1");
                await client.ListGrantablePermissionsAsync();
                await client.CreatePermissionAsync(new { resourceType = "Clients", operationType = "Read" });
                await client.UpdatePermissionAsync("perm_1", new { permit = false });
                await client.DeletePermissionAsync("perm_1");
                await client.ListRoleAssignmentsAsync();
                await client.CreateRoleAssignmentAsync(new { roleId = "role_1", principalType = "User", principalId = "usr_1" });
                await client.DeleteRoleAssignmentAsync("rassn_1");
                await client.CreateRoutingPolicyAsync(new { name = "Policy", modelName = "*", clientName = "*" });
                await client.ValidateRoutingPolicyAsync(new { name = "Policy", modelName = "*", clientName = "*" });
                await client.UpdateRoutingPolicyAsync("rpol_1", new { loadBalancingMode = "Random" });
                await client.DeleteRoutingPolicyAsync("rpol_1");

                Assert.Collection(
                    handler.Requests,
                    request => AssertRequest(request, HttpMethod.Post, "https://xeno.local/v1.0/api/tenants", "\"name\":\"Tenant\""),
                    request => AssertRequest(request, HttpMethod.Put, "https://xeno.local/v1.0/api/tenants/ten_1", "\"name\":\"Tenant 2\""),
                    request => AssertRequest(request, HttpMethod.Delete, "https://xeno.local/v1.0/api/tenants/ten_1"),
                    request => AssertRequest(request, HttpMethod.Post, "https://xeno.local/v1.0/api/users", "\"username\":\"user@xeno\""),
                    request => AssertRequest(request, HttpMethod.Put, "https://xeno.local/v1.0/api/users/usr_1", "\"active\":false"),
                    request => AssertRequest(request, HttpMethod.Delete, "https://xeno.local/v1.0/api/users/usr_1"),
                    request => AssertRequest(request, HttpMethod.Post, "https://xeno.local/v1.0/api/credentials", "\"userId\":\"usr_1\""),
                    request => AssertRequest(request, HttpMethod.Put, "https://xeno.local/v1.0/api/credentials/cred_1", "\"active\":true"),
                    request => AssertRequest(request, HttpMethod.Delete, "https://xeno.local/v1.0/api/credentials/cred_1"),
                    request => AssertRequest(request, HttpMethod.Post, "https://xeno.local/v1.0/api/roles", "\"name\":\"Operator\""),
                    request => AssertRequest(request, HttpMethod.Put, "https://xeno.local/v1.0/api/roles/role_1", "\"permissionIds\":[\"perm_1\"]"),
                    request => AssertRequest(request, HttpMethod.Delete, "https://xeno.local/v1.0/api/roles/role_1"),
                    request => AssertRequest(request, HttpMethod.Get, "https://xeno.local/v1.0/api/permissions/grantable"),
                    request => AssertRequest(request, HttpMethod.Post, "https://xeno.local/v1.0/api/permissions", "\"resourceType\":\"Clients\""),
                    request => AssertRequest(request, HttpMethod.Put, "https://xeno.local/v1.0/api/permissions/perm_1", "\"permit\":false"),
                    request => AssertRequest(request, HttpMethod.Delete, "https://xeno.local/v1.0/api/permissions/perm_1"),
                    request => AssertRequest(request, HttpMethod.Get, "https://xeno.local/v1.0/api/role-assignments"),
                    request => AssertRequest(request, HttpMethod.Post, "https://xeno.local/v1.0/api/role-assignments", "\"principalId\":\"usr_1\""),
                    request => AssertRequest(request, HttpMethod.Delete, "https://xeno.local/v1.0/api/role-assignments/rassn_1"),
                    request => AssertRequest(request, HttpMethod.Post, "https://xeno.local/v1.0/api/routing-policies", "\"modelName\":\"*\""),
                    request => AssertRequest(request, HttpMethod.Post, "https://xeno.local/v1.0/api/routing-policies/validate", "\"modelName\":\"*\""),
                    request => AssertRequest(request, HttpMethod.Put, "https://xeno.local/v1.0/api/routing-policies/rpol_1", "\"loadBalancingMode\":\"Random\""),
                    request => AssertRequest(request, HttpMethod.Delete, "https://xeno.local/v1.0/api/routing-policies/rpol_1"));
            }
        }

        [Fact]
        public async Task OpenAiAndHistoryMethods_UseExpectedRoutes()
        {
            RecordingHandler handler = new RecordingHandler(request =>
            {
                if (request.RequestUri?.AbsolutePath.Equals("/v1.0/api/request-history", StringComparison.OrdinalIgnoreCase) == true) return JsonResponse("[]");
                return JsonResponse("{}");
            });
            using (HttpClient httpClient = new HttpClient(handler))
            using (XenoClient client = new XenoClient("https://xeno.local", httpClient: httpClient))
            {
                await client.ListOpenAiModelsAsync();
                await client.CreateChatCompletionAsync(new { model = "llama3.2:latest" });
                await client.CreateEmbeddingAsync(new { model = "text-embedding-3-small", input = "hello" });
                await client.ListRequestHistoryAsync(new Dictionary<string, string> { ["model"] = "llama3.2:latest", ["empty"] = "" });
                await client.GetRequestHistorySummaryAsync(new Dictionary<string, string> { ["bucketMinutes"] = "60" });

                Assert.Collection(
                    handler.Requests,
                    request => AssertRequest(request, HttpMethod.Get, "https://xeno.local/v1/models"),
                    request => AssertRequest(request, HttpMethod.Post, "https://xeno.local/v1/chat/completions", "\"model\":\"llama3.2:latest\""),
                    request => AssertRequest(request, HttpMethod.Post, "https://xeno.local/v1/embeddings", "\"input\":\"hello\""),
                    request => AssertRequest(request, HttpMethod.Get, "https://xeno.local/v1.0/api/request-history?model=llama3.2%3Alatest"),
                    request => AssertRequest(request, HttpMethod.Get, "https://xeno.local/v1.0/api/request-history/summary?bucketMinutes=60"));
            }
        }

        [Fact]
        public async Task CredentialOptions_SendTenantAndCredentialHeaders()
        {
            RecordingHandler handler = new RecordingHandler(_ => JsonResponse("{}"));
            using (HttpClient httpClient = new HttpClient(handler))
            using (XenoClient client = new XenoClient(new XenoClientOptions
            {
                BaseUrl = "https://xeno.local",
                AccessKey = "xeno_access",
                TenantId = "ten_123",
                HttpClient = httpClient
            }))
            {
                await client.ListOpenAiModelsAsync();

                RecordedRequest request = Assert.Single(handler.Requests);
                Assert.Equal("xeno_access", request.AccessKey);
                Assert.Equal("ten_123", request.TenantId);
            }
        }

        [Fact]
        public async Task OpenAiErrors_SurfaceAuthenticationAndAuthorizationStatus()
        {
            RecordingHandler anonymousHandler = new RecordingHandler(_ => new HttpResponseMessage(HttpStatusCode.Unauthorized)
            {
                Content = new StringContent("{\"error\":\"Unauthenticated\",\"message\":\"Authentication is required.\"}", Encoding.UTF8, "application/json")
            });
            using (HttpClient anonymousHttpClient = new HttpClient(anonymousHandler))
            using (XenoClient anonymousClient = new XenoClient("https://xeno.local", httpClient: anonymousHttpClient))
            {
                XenoApiException unauthenticated = await Assert.ThrowsAsync<XenoApiException>(
                    async () => await anonymousClient.ListOpenAiModelsAsync());
                Assert.Equal(HttpStatusCode.Unauthorized, unauthenticated.StatusCode);
                Assert.Equal("/v1/models", unauthenticated.Endpoint);
            }

            RecordingHandler deniedHandler = new RecordingHandler(_ => new HttpResponseMessage(HttpStatusCode.Forbidden)
            {
                Content = new StringContent("{\"error\":\"PermissionDenied\",\"message\":\"Permission denied.\"}", Encoding.UTF8, "application/json")
            });
            using (HttpClient deniedHttpClient = new HttpClient(deniedHandler))
            using (XenoClient deniedClient = new XenoClient("https://xeno.local", "token-123", "ten_123", deniedHttpClient))
            {
                XenoApiException denied = await Assert.ThrowsAsync<XenoApiException>(
                    async () => await deniedClient.CreateChatCompletionAsync(new { model = "llama3.2:latest" }));
                Assert.Equal(HttpStatusCode.Forbidden, denied.StatusCode);
                Assert.Equal("/v1/chat/completions", denied.Endpoint);
            }
        }

        [Fact]
        public async Task ClientTelemetryMethods_UseExpectedRoutesAndBodies()
        {
            RecordingHandler handler = new RecordingHandler(request =>
            {
                if (request.Method == HttpMethod.Delete) return new HttpResponseMessage(HttpStatusCode.NoContent);
                if (request.RequestUri?.AbsolutePath.EndsWith("/rollups", StringComparison.OrdinalIgnoreCase) == true) return JsonResponse("[]");
                if (request.RequestUri?.AbsolutePath.EndsWith("/providers/latest", StringComparison.OrdinalIgnoreCase) == true) return JsonResponse("[]");
                if (request.RequestUri?.AbsolutePath.EndsWith("/collection-attempts", StringComparison.OrdinalIgnoreCase) == true) return JsonResponse("[]");
                if (request.RequestUri?.AbsolutePath.EndsWith("/metrics", StringComparison.OrdinalIgnoreCase) == true) return JsonResponse("[]");
                return JsonResponse("{}");
            });
            using (HttpClient httpClient = new HttpClient(handler))
            using (XenoClient client = new XenoClient("https://xeno.local", httpClient: httpClient))
            {
                await client.GetClientTelemetryStatusAsync();
                await client.GetLatestClientTelemetryAsync(new Dictionary<string, string> { ["clientId"] = "xcl_1", ["empty"] = "" });
                await client.CollectClientTelemetryAsync(new { clientId = "xcl_1", collectHostTelemetry = true });
                await client.CollectClientProviderTelemetryAsync(new { clientId = "xcl_1" });
                await client.SearchClientTelemetryAsync(new { clientId = "xcl_1", metric = "telemetry.cpu.utilizationPercent" });
                await client.GetClientTelemetryRollupsAsync(new { metric = "telemetry.cpu.utilizationPercent" });
                await client.GetClientTelemetryDetailAsync("ctel_1");
                await client.DeleteClientTelemetryAsync("ctel_1");
                await client.BulkDeleteClientTelemetryAsync(new { clientId = "xcl_1" });
                await client.GetLatestClientProviderTelemetryAsync(new Dictionary<string, string> { ["clientId"] = "xcl_1" });
                await client.SearchClientProviderTelemetryAsync(new { providerName = "ollama" });
                await client.GetClientProviderTelemetryRollupsAsync(new { metric = "provider.queuedRequests" });
                await client.GetClientProviderTelemetryDetailAsync("cptel_1");
                await client.DeleteClientProviderTelemetryAsync("cptel_1");
                await client.BulkDeleteClientProviderTelemetryAsync(new { providerName = "ollama" });
                await client.ListClientTelemetryCollectionAttemptsAsync();
                await client.GetClientTelemetryMetricCatalogAsync();

                Assert.Collection(
                    handler.Requests,
                    request => AssertRequest(request, HttpMethod.Get, "https://xeno.local/v1.0/api/client-telemetry/status"),
                    request => AssertRequest(request, HttpMethod.Get, "https://xeno.local/v1.0/api/client-telemetry/latest?clientId=xcl_1"),
                    request => AssertRequest(request, HttpMethod.Post, "https://xeno.local/v1.0/api/client-telemetry/collect", "\"clientId\":\"xcl_1\""),
                    request => AssertRequest(request, HttpMethod.Post, "https://xeno.local/v1.0/api/client-telemetry/providers/collect", "\"clientId\":\"xcl_1\""),
                    request => AssertRequest(request, HttpMethod.Post, "https://xeno.local/v1.0/api/client-telemetry/history/search", "\"metric\":\"telemetry.cpu.utilizationPercent\""),
                    request => AssertRequest(request, HttpMethod.Post, "https://xeno.local/v1.0/api/client-telemetry/history/rollups", "\"metric\":\"telemetry.cpu.utilizationPercent\""),
                    request => AssertRequest(request, HttpMethod.Get, "https://xeno.local/v1.0/api/client-telemetry/history/ctel_1"),
                    request => AssertRequest(request, HttpMethod.Delete, "https://xeno.local/v1.0/api/client-telemetry/history/ctel_1"),
                    request => AssertRequest(request, HttpMethod.Post, "https://xeno.local/v1.0/api/client-telemetry/history/delete", "\"clientId\":\"xcl_1\""),
                    request => AssertRequest(request, HttpMethod.Get, "https://xeno.local/v1.0/api/client-telemetry/providers/latest?clientId=xcl_1"),
                    request => AssertRequest(request, HttpMethod.Post, "https://xeno.local/v1.0/api/client-telemetry/providers/history/search", "\"providerName\":\"ollama\""),
                    request => AssertRequest(request, HttpMethod.Post, "https://xeno.local/v1.0/api/client-telemetry/providers/history/rollups", "\"metric\":\"provider.queuedRequests\""),
                    request => AssertRequest(request, HttpMethod.Get, "https://xeno.local/v1.0/api/client-telemetry/providers/history/cptel_1"),
                    request => AssertRequest(request, HttpMethod.Delete, "https://xeno.local/v1.0/api/client-telemetry/providers/history/cptel_1"),
                    request => AssertRequest(request, HttpMethod.Post, "https://xeno.local/v1.0/api/client-telemetry/providers/history/delete", "\"providerName\":\"ollama\""),
                    request => AssertRequest(request, HttpMethod.Get, "https://xeno.local/v1.0/api/client-telemetry/collection-attempts"),
                    request => AssertRequest(request, HttpMethod.Get, "https://xeno.local/v1.0/api/client-telemetry/metrics"));
            }
        }

        [Fact]
        public async Task GetMetricsAsync_ReturnsText()
        {
            RecordingHandler handler = new RecordingHandler(_ => new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent("xeno_connected_clients 1", Encoding.UTF8, "text/plain")
            });
            using (HttpClient httpClient = new HttpClient(handler))
            using (XenoClient client = new XenoClient("https://xeno.local", httpClient: httpClient))
            {
                string metrics = await client.GetMetricsAsync();

                Assert.Contains("xeno_connected_clients", metrics);
                Assert.Equal("https://xeno.local/metrics", handler.Requests[0].Uri?.ToString());
            }
        }

        [Fact]
        public async Task ApiError_ThrowsXenoApiException()
        {
            RecordingHandler handler = new RecordingHandler(_ => new HttpResponseMessage(HttpStatusCode.Forbidden)
            {
                Content = new StringContent("{\"message\":\"denied\"}", Encoding.UTF8, "application/json")
            });
            using (HttpClient httpClient = new HttpClient(handler))
            using (XenoClient client = new XenoClient("https://xeno.local", httpClient: httpClient))
            {
                XenoApiException exception = await Assert.ThrowsAsync<XenoApiException>(
                    async () => await client.ListClientsAsync());

                Assert.Equal(HttpStatusCode.Forbidden, exception.StatusCode);
                Assert.Equal("/v1.0/api/clients", exception.Endpoint);
                Assert.Contains("denied", exception.Message);
            }
        }

        [Fact]
        public async Task Request_PropagatesCancellationToken()
        {
            RecordingHandler handler = new RecordingHandler(_ => JsonResponse("{}"));
            using (HttpClient httpClient = new HttpClient(handler))
            using (XenoClient client = new XenoClient("https://xeno.local", httpClient: httpClient))
            using (CancellationTokenSource source = new CancellationTokenSource())
            {
                SettingsResponse response = await client.GetSettingsAsync(source.Token);
                Assert.NotNull(response);

                Assert.True(handler.Requests[0].CancellationToken.CanBeCanceled);
            }
        }

        [Fact]
        public async Task TypedResponses_AreMaterialized()
        {
            RecordingHandler handler = new RecordingHandler(_ => JsonResponse("{\"status\":\"ok\",\"product\":\"Xeno\",\"version\":\"0.1.0\"}"));
            using (HttpClient httpClient = new HttpClient(handler))
            using (XenoClient client = new XenoClient("https://xeno.local", httpClient: httpClient))
            {
                HealthResponse response = await client.GetHealthAsync();

                Assert.Equal("ok", response.Status);
                Assert.Equal("Xeno", response.Product);
            }
        }

        private static HttpResponseMessage JsonResponse(string json)
        {
            return new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(json, Encoding.UTF8, "application/json")
            };
        }

        private static void AssertRequest(RecordedRequest request, HttpMethod method, string uri, string? bodyContains = null)
        {
            Assert.Equal(method, request.Method);
            Assert.Equal(uri, request.Uri?.ToString());
            if (bodyContains != null) Assert.Contains(bodyContains, request.Body);
        }
    }
}
