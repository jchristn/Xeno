namespace Xeno.Sdk
{
    using System;
    using System.Collections.Generic;
    using System.Globalization;
    using System.Net.Http;
    using System.Net.Http.Headers;
    using System.Text;
    using System.Text.Json;
    using System.Threading;
    using System.Threading.Tasks;

    /// <summary>
    /// Lightweight Xeno REST and OpenAI-compatible API client.
    /// </summary>
    /// <remarks>
    /// This type is not thread-affine. Concurrent calls are safe when the supplied <see cref="HttpClient"/> is safe.
    /// </remarks>
    public sealed class XenoClient : IDisposable
    {
        private readonly HttpClient _HttpClient;
        private readonly bool _OwnsHttpClient;
        private readonly JsonSerializerOptions _JsonOptions = new JsonSerializerOptions
        {
            PropertyNamingPolicy = null
        };
        private bool _Disposed;

        /// <summary>
        /// Initializes a new instance of the <see cref="XenoClient"/> class.
        /// </summary>
        /// <param name="baseUrl">Xeno server base URL.</param>
        /// <param name="bearerToken">Optional bearer token.</param>
        /// <param name="tenantId">Tenant identifier. Default value is <c>ten_default</c>.</param>
        /// <param name="httpClient">Optional caller-owned HTTP client.</param>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="baseUrl"/> is null or empty.</exception>
        public XenoClient(string baseUrl, string? bearerToken = null, string tenantId = "ten_default", HttpClient? httpClient = null)
            : this(new XenoClientOptions
            {
                BaseUrl = baseUrl,
                BearerToken = bearerToken,
                TenantId = tenantId,
                HttpClient = httpClient
            })
        {
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="XenoClient"/> class.
        /// </summary>
        /// <param name="options">Client options.</param>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="options"/> is null.</exception>
        /// <exception cref="ArgumentException">Thrown when the base URL is empty.</exception>
        public XenoClient(XenoClientOptions options)
        {
            ArgumentNullException.ThrowIfNull(options);
            if (String.IsNullOrWhiteSpace(options.BaseUrl)) throw new ArgumentException("Base URL is required.", nameof(options));

            _HttpClient = options.HttpClient ?? new HttpClient();
            _OwnsHttpClient = options.HttpClient == null;
            _HttpClient.BaseAddress = new Uri(options.BaseUrl.TrimEnd('/') + "/", UriKind.Absolute);

            if (!String.IsNullOrWhiteSpace(options.BearerToken))
            {
                _HttpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", options.BearerToken);
            }

            if (!String.IsNullOrWhiteSpace(options.TenantId) && !_HttpClient.DefaultRequestHeaders.Contains("x-tenant-guid"))
            {
                _HttpClient.DefaultRequestHeaders.Add("x-tenant-guid", options.TenantId);
            }
        }

        /// <summary>
        /// Gets server health.
        /// </summary>
        /// <param name="token">Cancellation token.</param>
        /// <returns>Health response JSON.</returns>
        public async Task<JsonDocument> GetHealthAsync(CancellationToken token = default)
        {
            return await GetJsonAsync("/health", token).ConfigureAwait(false);
        }

        /// <summary>
        /// Gets the OpenAPI document.
        /// </summary>
        /// <param name="token">Cancellation token.</param>
        /// <returns>OpenAPI response JSON.</returns>
        public async Task<JsonDocument> GetOpenApiAsync(CancellationToken token = default)
        {
            return await GetJsonAsync("/openapi.json", token).ConfigureAwait(false);
        }

        /// <summary>
        /// Gets Prometheus-style metrics text.
        /// </summary>
        /// <param name="token">Cancellation token.</param>
        /// <returns>Metrics text.</returns>
        public async Task<string> GetMetricsAsync(CancellationToken token = default)
        {
            return await SendTextAsync(HttpMethod.Get, "/metrics", null, token).ConfigureAwait(false);
        }

        /// <summary>
        /// Logs in with a user password and receives a one-time-visible client credential.
        /// </summary>
        /// <param name="username">Username or email.</param>
        /// <param name="password">Password.</param>
        /// <param name="tenantId">Tenant identifier.</param>
        /// <param name="token">Cancellation token.</param>
        /// <returns>Credential response JSON.</returns>
        public async Task<JsonDocument> LoginAsync(string username, string password, string tenantId = "ten_default", CancellationToken token = default)
        {
            return await PostJsonAsync("/v1.0/api/auth/login", new { username, password, tenantId }, token).ConfigureAwait(false);
        }

        /// <summary>
        /// Creates a bearer token from a direct access credential.
        /// </summary>
        /// <param name="accessKey">Access key.</param>
        /// <param name="secretKey">Secret key.</param>
        /// <param name="token">Cancellation token.</param>
        /// <returns>Token response JSON.</returns>
        public async Task<JsonDocument> CreateTokenAsync(string accessKey, string secretKey, CancellationToken token = default)
        {
            return await PostJsonAsync("/v1.0/api/auth/token", new { accessKey, secretKey }, token).ConfigureAwait(false);
        }

        /// <summary>
        /// Gets bearer token details for the current request context.
        /// </summary>
        /// <param name="token">Cancellation token.</param>
        /// <returns>Token details JSON.</returns>
        public async Task<JsonDocument> GetTokenDetailsAsync(CancellationToken token = default)
        {
            return await GetJsonAsync("/v1.0/api/auth/token/details", token).ConfigureAwait(false);
        }

        /// <summary>
        /// Refreshes the active bearer token.
        /// </summary>
        /// <param name="token">Cancellation token.</param>
        /// <returns>New token response JSON.</returns>
        public async Task<JsonDocument> RefreshTokenAsync(CancellationToken token = default)
        {
            return await PostJsonAsync("/v1.0/api/auth/token/refresh", new object(), token).ConfigureAwait(false);
        }

        /// <summary>
        /// Logs out the current bearer token.
        /// </summary>
        /// <param name="token">Cancellation token.</param>
        /// <returns>Logout response JSON.</returns>
        public async Task<JsonDocument> LogoutAsync(CancellationToken token = default)
        {
            return await PostJsonAsync("/v1.0/api/auth/logout", new object(), token).ConfigureAwait(false);
        }

        /// <summary>
        /// Lists tenants.
        /// </summary>
        /// <param name="token">Cancellation token.</param>
        /// <returns>Tenant array JSON.</returns>
        public async Task<JsonDocument> ListTenantsAsync(CancellationToken token = default)
        {
            return await GetJsonAsync("/v1.0/api/tenants", token).ConfigureAwait(false);
        }

        /// <summary>
        /// Creates a tenant.
        /// </summary>
        /// <param name="tenant">Tenant payload.</param>
        /// <param name="token">Cancellation token.</param>
        /// <returns>Tenant JSON.</returns>
        public async Task<JsonDocument> CreateTenantAsync(object tenant, CancellationToken token = default)
        {
            return await PostJsonAsync("/v1.0/api/tenants", tenant ?? new object(), token).ConfigureAwait(false);
        }

        /// <summary>
        /// Updates a tenant.
        /// </summary>
        /// <param name="id">Tenant identifier.</param>
        /// <param name="tenant">Tenant payload.</param>
        /// <param name="token">Cancellation token.</param>
        /// <returns>Tenant JSON.</returns>
        public async Task<JsonDocument> UpdateTenantAsync(string id, object tenant, CancellationToken token = default)
        {
            return await PutJsonAsync("/v1.0/api/tenants/" + Escape(id), tenant ?? new object(), token).ConfigureAwait(false);
        }

        /// <summary>
        /// Deletes a tenant.
        /// </summary>
        /// <param name="id">Tenant identifier.</param>
        /// <param name="token">Cancellation token.</param>
        /// <returns>Task.</returns>
        public async Task DeleteTenantAsync(string id, CancellationToken token = default)
        {
            await DeleteAsync("/v1.0/api/tenants/" + Escape(id), token).ConfigureAwait(false);
        }

        /// <summary>
        /// Lists users.
        /// </summary>
        /// <param name="token">Cancellation token.</param>
        /// <returns>User array JSON.</returns>
        public async Task<JsonDocument> ListUsersAsync(CancellationToken token = default)
        {
            return await GetJsonAsync("/v1.0/api/users", token).ConfigureAwait(false);
        }

        /// <summary>
        /// Creates a user.
        /// </summary>
        /// <param name="user">User payload.</param>
        /// <param name="token">Cancellation token.</param>
        /// <returns>User JSON.</returns>
        public async Task<JsonDocument> CreateUserAsync(object user, CancellationToken token = default)
        {
            return await PostJsonAsync("/v1.0/api/users", user ?? new object(), token).ConfigureAwait(false);
        }

        /// <summary>
        /// Updates a user.
        /// </summary>
        /// <param name="id">User identifier.</param>
        /// <param name="user">User payload.</param>
        /// <param name="token">Cancellation token.</param>
        /// <returns>User JSON.</returns>
        public async Task<JsonDocument> UpdateUserAsync(string id, object user, CancellationToken token = default)
        {
            return await PutJsonAsync("/v1.0/api/users/" + Escape(id), user ?? new object(), token).ConfigureAwait(false);
        }

        /// <summary>
        /// Deletes a user.
        /// </summary>
        /// <param name="id">User identifier.</param>
        /// <param name="token">Cancellation token.</param>
        /// <returns>Task.</returns>
        public async Task DeleteUserAsync(string id, CancellationToken token = default)
        {
            await DeleteAsync("/v1.0/api/users/" + Escape(id), token).ConfigureAwait(false);
        }

        /// <summary>
        /// Lists credentials with redacted secrets.
        /// </summary>
        /// <param name="token">Cancellation token.</param>
        /// <returns>Credential array JSON.</returns>
        public async Task<JsonDocument> ListCredentialsAsync(CancellationToken token = default)
        {
            return await GetJsonAsync("/v1.0/api/credentials", token).ConfigureAwait(false);
        }

        /// <summary>
        /// Creates a credential and returns the secret once.
        /// </summary>
        /// <param name="credential">Credential payload.</param>
        /// <param name="token">Cancellation token.</param>
        /// <returns>Credential JSON.</returns>
        public async Task<JsonDocument> CreateCredentialAsync(object credential, CancellationToken token = default)
        {
            return await PostJsonAsync("/v1.0/api/credentials", credential ?? new object(), token).ConfigureAwait(false);
        }

        /// <summary>
        /// Updates a credential.
        /// </summary>
        /// <param name="id">Credential identifier.</param>
        /// <param name="credential">Credential payload.</param>
        /// <param name="token">Cancellation token.</param>
        /// <returns>Credential JSON.</returns>
        public async Task<JsonDocument> UpdateCredentialAsync(string id, object credential, CancellationToken token = default)
        {
            return await PutJsonAsync("/v1.0/api/credentials/" + Escape(id), credential ?? new object(), token).ConfigureAwait(false);
        }

        /// <summary>
        /// Deletes a credential.
        /// </summary>
        /// <param name="id">Credential identifier.</param>
        /// <param name="token">Cancellation token.</param>
        /// <returns>Task.</returns>
        public async Task DeleteCredentialAsync(string id, CancellationToken token = default)
        {
            await DeleteAsync("/v1.0/api/credentials/" + Escape(id), token).ConfigureAwait(false);
        }

        /// <summary>
        /// Lists roles.
        /// </summary>
        /// <param name="token">Cancellation token.</param>
        /// <returns>Role array JSON.</returns>
        public async Task<JsonDocument> ListRolesAsync(CancellationToken token = default)
        {
            return await GetJsonAsync("/v1.0/api/roles", token).ConfigureAwait(false);
        }

        /// <summary>
        /// Creates a role.
        /// </summary>
        /// <param name="role">Role payload.</param>
        /// <param name="token">Cancellation token.</param>
        /// <returns>Role JSON.</returns>
        public async Task<JsonDocument> CreateRoleAsync(object role, CancellationToken token = default)
        {
            return await PostJsonAsync("/v1.0/api/roles", role ?? new object(), token).ConfigureAwait(false);
        }

        /// <summary>
        /// Updates a role.
        /// </summary>
        /// <param name="id">Role identifier.</param>
        /// <param name="role">Role payload.</param>
        /// <param name="token">Cancellation token.</param>
        /// <returns>Role JSON.</returns>
        public async Task<JsonDocument> UpdateRoleAsync(string id, object role, CancellationToken token = default)
        {
            return await PutJsonAsync("/v1.0/api/roles/" + Escape(id), role ?? new object(), token).ConfigureAwait(false);
        }

        /// <summary>
        /// Deletes a role.
        /// </summary>
        /// <param name="id">Role identifier.</param>
        /// <param name="token">Cancellation token.</param>
        /// <returns>Task.</returns>
        public async Task DeleteRoleAsync(string id, CancellationToken token = default)
        {
            await DeleteAsync("/v1.0/api/roles/" + Escape(id), token).ConfigureAwait(false);
        }

        /// <summary>
        /// Lists permissions.
        /// </summary>
        /// <param name="token">Cancellation token.</param>
        /// <returns>Permission array JSON.</returns>
        public async Task<JsonDocument> ListPermissionsAsync(CancellationToken token = default)
        {
            return await GetJsonAsync("/v1.0/api/permissions", token).ConfigureAwait(false);
        }

        /// <summary>
        /// Creates a permission.
        /// </summary>
        /// <param name="permission">Permission payload.</param>
        /// <param name="token">Cancellation token.</param>
        /// <returns>Permission JSON.</returns>
        public async Task<JsonDocument> CreatePermissionAsync(object permission, CancellationToken token = default)
        {
            return await PostJsonAsync("/v1.0/api/permissions", permission ?? new object(), token).ConfigureAwait(false);
        }

        /// <summary>
        /// Updates a permission.
        /// </summary>
        /// <param name="id">Permission identifier.</param>
        /// <param name="permission">Permission payload.</param>
        /// <param name="token">Cancellation token.</param>
        /// <returns>Permission JSON.</returns>
        public async Task<JsonDocument> UpdatePermissionAsync(string id, object permission, CancellationToken token = default)
        {
            return await PutJsonAsync("/v1.0/api/permissions/" + Escape(id), permission ?? new object(), token).ConfigureAwait(false);
        }

        /// <summary>
        /// Deletes a permission.
        /// </summary>
        /// <param name="id">Permission identifier.</param>
        /// <param name="token">Cancellation token.</param>
        /// <returns>Task.</returns>
        public async Task DeletePermissionAsync(string id, CancellationToken token = default)
        {
            await DeleteAsync("/v1.0/api/permissions/" + Escape(id), token).ConfigureAwait(false);
        }

        /// <summary>
        /// Lists effective permissions for the request context.
        /// </summary>
        /// <param name="token">Cancellation token.</param>
        /// <returns>Effective permission array JSON.</returns>
        public async Task<JsonDocument> ListEffectivePermissionsAsync(CancellationToken token = default)
        {
            return await GetJsonAsync("/v1.0/api/effective-permissions", token).ConfigureAwait(false);
        }

        /// <summary>
        /// Lists audit records.
        /// </summary>
        /// <param name="token">Cancellation token.</param>
        /// <returns>Audit array JSON.</returns>
        public async Task<JsonDocument> ListAuditRecordsAsync(CancellationToken token = default)
        {
            return await GetJsonAsync("/v1.0/api/audit", token).ConfigureAwait(false);
        }

        /// <summary>
        /// Deletes an audit record.
        /// </summary>
        /// <param name="id">Audit record identifier.</param>
        /// <param name="token">Cancellation token.</param>
        /// <returns>Task.</returns>
        public async Task DeleteAuditRecordAsync(string id, CancellationToken token = default)
        {
            await DeleteAsync("/v1.0/api/audit/" + Escape(id), token).ConfigureAwait(false);
        }

        /// <summary>
        /// Lists connected or registered clients.
        /// </summary>
        /// <param name="token">Cancellation token.</param>
        /// <returns>Client array JSON.</returns>
        public async Task<JsonDocument> ListClientsAsync(CancellationToken token = default)
        {
            return await GetJsonAsync("/v1.0/api/clients", token).ConfigureAwait(false);
        }

        /// <summary>
        /// Deletes a client record.
        /// </summary>
        /// <param name="id">Client identifier.</param>
        /// <param name="token">Cancellation token.</param>
        /// <returns>Task.</returns>
        public async Task DeleteClientAsync(string id, CancellationToken token = default)
        {
            await DeleteAsync("/v1.0/api/clients/" + Escape(id), token).ConfigureAwait(false);
        }

        /// <summary>
        /// Deletes one model advertised by a client.
        /// </summary>
        /// <param name="model">Client model delete payload.</param>
        /// <param name="token">Cancellation token.</param>
        /// <returns>Delete response JSON.</returns>
        public async Task<JsonDocument> DeleteClientModelAsync(object model, CancellationToken token = default)
        {
            return await DeleteJsonAsync("/v1.0/api/client-models", model ?? new object(), token).ConfigureAwait(false);
        }

        /// <summary>
        /// Lists model definitions.
        /// </summary>
        /// <param name="token">Cancellation token.</param>
        /// <returns>Model definition array JSON.</returns>
        public async Task<JsonDocument> ListModelDefinitionsAsync(CancellationToken token = default)
        {
            return await GetJsonAsync("/v1.0/api/model-definitions", token).ConfigureAwait(false);
        }

        /// <summary>
        /// Lists model configurations.
        /// </summary>
        /// <param name="token">Cancellation token.</param>
        /// <returns>Model configuration array JSON.</returns>
        public async Task<JsonDocument> ListModelConfigurationsAsync(CancellationToken token = default)
        {
            return await GetJsonAsync("/v1.0/api/model-configurations", token).ConfigureAwait(false);
        }

        /// <summary>
        /// Lists OpenAI-compatible models.
        /// </summary>
        /// <param name="token">Cancellation token.</param>
        /// <returns>OpenAI model list JSON.</returns>
        public async Task<JsonDocument> ListOpenAiModelsAsync(CancellationToken token = default)
        {
            return await GetJsonAsync("/v1/models", token).ConfigureAwait(false);
        }

        /// <summary>
        /// Creates an OpenAI-compatible chat completion.
        /// </summary>
        /// <param name="request">Chat completion payload.</param>
        /// <param name="token">Cancellation token.</param>
        /// <returns>Completion response JSON.</returns>
        public async Task<JsonDocument> CreateChatCompletionAsync(object request, CancellationToken token = default)
        {
            return await PostJsonAsync("/v1/chat/completions", request ?? new object(), token).ConfigureAwait(false);
        }

        /// <summary>
        /// Creates an OpenAI-compatible completion.
        /// </summary>
        /// <param name="request">Completion payload.</param>
        /// <param name="token">Cancellation token.</param>
        /// <returns>Completion response JSON.</returns>
        public async Task<JsonDocument> CreateCompletionAsync(object request, CancellationToken token = default)
        {
            return await PostJsonAsync("/v1/completions", request ?? new object(), token).ConfigureAwait(false);
        }

        /// <summary>
        /// Creates OpenAI-compatible embeddings.
        /// </summary>
        /// <param name="request">Embedding payload.</param>
        /// <param name="token">Cancellation token.</param>
        /// <returns>Embedding response JSON.</returns>
        public async Task<JsonDocument> CreateEmbeddingAsync(object request, CancellationToken token = default)
        {
            return await PostJsonAsync("/v1/embeddings", request ?? new object(), token).ConfigureAwait(false);
        }

        /// <summary>
        /// Lists request history.
        /// </summary>
        /// <param name="filters">Optional query-string filters.</param>
        /// <param name="token">Cancellation token.</param>
        /// <returns>Request history array JSON.</returns>
        public async Task<JsonDocument> ListRequestHistoryAsync(IDictionary<string, string>? filters = null, CancellationToken token = default)
        {
            return await GetJsonAsync("/v1.0/api/request-history" + QueryString(filters), token).ConfigureAwait(false);
        }

        /// <summary>
        /// Gets a request history entry.
        /// </summary>
        /// <param name="id">Request history identifier.</param>
        /// <param name="token">Cancellation token.</param>
        /// <returns>Request history entry JSON.</returns>
        public async Task<JsonDocument> GetRequestHistoryAsync(string id, CancellationToken token = default)
        {
            return await GetJsonAsync("/v1.0/api/request-history/" + Escape(id), token).ConfigureAwait(false);
        }

        /// <summary>
        /// Deletes a request history entry.
        /// </summary>
        /// <param name="id">Request history identifier.</param>
        /// <param name="token">Cancellation token.</param>
        /// <returns>Task.</returns>
        public async Task DeleteRequestHistoryAsync(string id, CancellationToken token = default)
        {
            await DeleteAsync("/v1.0/api/request-history/" + Escape(id), token).ConfigureAwait(false);
        }

        /// <summary>
        /// Gets a request history summary.
        /// </summary>
        /// <param name="filters">Optional query-string filters.</param>
        /// <param name="token">Cancellation token.</param>
        /// <returns>Summary JSON.</returns>
        public async Task<JsonDocument> GetRequestHistorySummaryAsync(IDictionary<string, string>? filters = null, CancellationToken token = default)
        {
            return await GetJsonAsync("/v1.0/api/request-history/summary" + QueryString(filters), token).ConfigureAwait(false);
        }

        /// <summary>
        /// Gets request analytics overview.
        /// </summary>
        /// <param name="token">Cancellation token.</param>
        /// <returns>Overview JSON.</returns>
        public async Task<JsonDocument> GetRequestAnalyticsOverviewAsync(CancellationToken token = default)
        {
            return await GetJsonAsync("/v1.0/api/request-history/analytics/overview", token).ConfigureAwait(false);
        }

        /// <summary>
        /// Bulk deletes request history.
        /// </summary>
        /// <param name="filter">Delete filter.</param>
        /// <param name="token">Cancellation token.</param>
        /// <returns>Delete response JSON.</returns>
        public async Task<JsonDocument> BulkDeleteRequestHistoryAsync(object filter, CancellationToken token = default)
        {
            return await PostJsonAsync("/v1.0/api/request-history/bulk", filter ?? new object(), token).ConfigureAwait(false);
        }

        /// <summary>
        /// Gets analytics overview.
        /// </summary>
        /// <param name="token">Cancellation token.</param>
        /// <returns>Overview JSON.</returns>
        public async Task<JsonDocument> GetAnalyticsOverviewAsync(CancellationToken token = default)
        {
            return await GetJsonAsync("/v1.0/api/analytics/overview", token).ConfigureAwait(false);
        }

        /// <summary>
        /// Gets server settings.
        /// </summary>
        /// <param name="token">Cancellation token.</param>
        /// <returns>Settings response JSON.</returns>
        public async Task<JsonDocument> GetSettingsAsync(CancellationToken token = default)
        {
            return await GetJsonAsync("/v1.0/api/settings", token).ConfigureAwait(false);
        }

        /// <summary>
        /// Updates server settings.
        /// </summary>
        /// <param name="settings">Settings payload.</param>
        /// <param name="token">Cancellation token.</param>
        /// <returns>Settings response JSON.</returns>
        public async Task<JsonDocument> UpdateSettingsAsync(object settings, CancellationToken token = default)
        {
            return await PostJsonAsync("/v1.0/api/settings", settings ?? new object(), token).ConfigureAwait(false);
        }

        /// <summary>
        /// Gets platform backup.
        /// </summary>
        /// <param name="token">Cancellation token.</param>
        /// <returns>Backup JSON.</returns>
        public async Task<JsonDocument> GetBackupAsync(CancellationToken token = default)
        {
            return await GetJsonAsync("/v1.0/api/backup", token).ConfigureAwait(false);
        }

        /// <summary>
        /// Restores platform backup.
        /// </summary>
        /// <param name="backup">Backup payload.</param>
        /// <param name="token">Cancellation token.</param>
        /// <returns>Restore response JSON.</returns>
        public async Task<JsonDocument> RestoreBackupAsync(object backup, CancellationToken token = default)
        {
            return await PostJsonAsync("/v1.0/api/restore", backup ?? new object(), token).ConfigureAwait(false);
        }

        /// <summary>
        /// Releases resources used by the client.
        /// </summary>
        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        private void Dispose(bool disposing)
        {
            if (_Disposed) return;
            if (disposing && _OwnsHttpClient) _HttpClient.Dispose();
            _Disposed = true;
        }

        private async Task<JsonDocument> GetJsonAsync(string endpoint, CancellationToken token)
        {
            string json = await SendTextAsync(HttpMethod.Get, endpoint, null, token).ConfigureAwait(false);
            return JsonDocument.Parse(json);
        }

        private async Task<JsonDocument> PostJsonAsync(string endpoint, object body, CancellationToken token)
        {
            string json = await SendTextAsync(HttpMethod.Post, endpoint, body, token).ConfigureAwait(false);
            return JsonDocument.Parse(json);
        }

        private async Task<JsonDocument> PutJsonAsync(string endpoint, object body, CancellationToken token)
        {
            string json = await SendTextAsync(HttpMethod.Put, endpoint, body, token).ConfigureAwait(false);
            return JsonDocument.Parse(json);
        }

        private async Task<JsonDocument> DeleteJsonAsync(string endpoint, object body, CancellationToken token)
        {
            string json = await SendTextAsync(HttpMethod.Delete, endpoint, body, token).ConfigureAwait(false);
            return JsonDocument.Parse(json);
        }

        private async Task DeleteAsync(string endpoint, CancellationToken token)
        {
            await SendTextAsync(HttpMethod.Delete, endpoint, null, token).ConfigureAwait(false);
        }

        private async Task<string> SendTextAsync(HttpMethod method, string endpoint, object? body, CancellationToken token)
        {
            token.ThrowIfCancellationRequested();
            using HttpRequestMessage request = new HttpRequestMessage(method, endpoint.TrimStart('/'));
            if (body != null)
            {
                string json = JsonSerializer.Serialize(body, _JsonOptions);
                request.Content = new StringContent(json, Encoding.UTF8, "application/json");
            }

            using HttpResponseMessage response = await _HttpClient.SendAsync(request, token).ConfigureAwait(false);
            string responseBody = response.Content == null
                ? String.Empty
                : await response.Content.ReadAsStringAsync(token).ConfigureAwait(false);

            if (!response.IsSuccessStatusCode)
            {
                throw new XenoApiException(ExtractErrorMessage(responseBody, response.StatusCode.ToString()), response.StatusCode, endpoint, responseBody);
            }

            return String.IsNullOrWhiteSpace(responseBody) ? "{}" : responseBody;
        }

        private static string Escape(string input)
        {
            if (String.IsNullOrWhiteSpace(input)) throw new ArgumentException("Identifier is required.", nameof(input));
            return Uri.EscapeDataString(input);
        }

        private static string QueryString(IDictionary<string, string>? filters)
        {
            if (filters == null || filters.Count == 0) return String.Empty;

            List<string> parts = new List<string>();
            foreach (KeyValuePair<string, string> filter in filters)
            {
                if (String.IsNullOrWhiteSpace(filter.Value)) continue;
                parts.Add(Uri.EscapeDataString(filter.Key) + "=" + Uri.EscapeDataString(filter.Value));
            }

            return parts.Count == 0 ? String.Empty : "?" + String.Join("&", parts);
        }

        private static string ExtractErrorMessage(string body, string fallback)
        {
            if (String.IsNullOrWhiteSpace(body)) return "Xeno API request failed with status " + fallback + ".";
            try
            {
                using JsonDocument document = JsonDocument.Parse(body);
                if (document.RootElement.TryGetProperty("message", out JsonElement message) && message.ValueKind == JsonValueKind.String)
                {
                    return message.GetString() ?? fallback;
                }

                if (document.RootElement.TryGetProperty("error", out JsonElement error) && error.ValueKind == JsonValueKind.String)
                {
                    return error.GetString() ?? fallback;
                }
            }
            catch (JsonException)
            {
                return body;
            }

            return fallback;
        }
    }
}
