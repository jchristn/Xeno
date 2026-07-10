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
        private bool _Disposed;

        /// <summary>
        /// Initializes a new instance of the <see cref="XenoClient"/> class.
        /// </summary>
        /// <param name="baseUrl">Xeno server base URL.</param>
        /// <param name="bearerToken">Bearer token. Default value is <c>default</c>; use <c>null</c> to omit bearer authentication.</param>
        /// <param name="tenantId">Optional tenant identifier. Default value is <c>null</c>; bearer credentials infer tenant context.</param>
        /// <param name="httpClient">Optional caller-owned HTTP client.</param>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="baseUrl"/> is null or empty.</exception>
        public XenoClient(string baseUrl, string? bearerToken = "default", string? tenantId = null, HttpClient? httpClient = null)
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

            if (!String.IsNullOrWhiteSpace(options.AccessKey) && !_HttpClient.DefaultRequestHeaders.Contains("x-access-key"))
            {
                _HttpClient.DefaultRequestHeaders.Add("x-access-key", options.AccessKey);
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
        public async Task<HealthResponse> GetHealthAsync(CancellationToken token = default)
        {
            return await GetJsonAsync<HealthResponse>("/health", token).ConfigureAwait(false);
        }

        /// <summary>
        /// Gets the OpenAPI document.
        /// </summary>
        /// <param name="token">Cancellation token.</param>
        /// <returns>OpenAPI response JSON.</returns>
        public async Task<XenoObjectResponse> GetOpenApiAsync(CancellationToken token = default)
        {
            return await GetJsonAsync<XenoObjectResponse>("/openapi.json", token).ConfigureAwait(false);
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
        public async Task<CredentialLoginResponse> LoginAsync(string username, string password, string tenantId = "ten_default", CancellationToken token = default)
        {
            return await PostJsonAsync<CredentialLoginResponse>("/v1.0/api/auth/login", new { username, password, tenantId }, token).ConfigureAwait(false);
        }

        /// <summary>
        /// Onboards a new tenant administrator or a regular user in an existing tenant.
        /// </summary>
        /// <param name="request">Onboarding request. Omit tenant ID to create a tenant; include tenant ID to add a regular user.</param>
        /// <param name="token">Cancellation token.</param>
        /// <returns>Onboarding response JSON.</returns>
        public async Task<AuthOnboardingResponse> OnboardAsync(AuthOnboardingRequest request, CancellationToken token = default)
        {
            return await PostJsonAsync<AuthOnboardingResponse>("/v1.0/api/onboarding/signup", request ?? new AuthOnboardingRequest(), token).ConfigureAwait(false);
        }

        /// <summary>
        /// Creates a bearer token from a direct access credential.
        /// </summary>
        /// <param name="accessKey">Access key.</param>
        /// <param name="token">Cancellation token.</param>
        /// <returns>Token response JSON.</returns>
        public async Task<TokenResponse> CreateTokenAsync(string accessKey, CancellationToken token = default)
        {
            return await PostJsonAsync<TokenResponse>("/v1.0/api/auth/token", new { accessKey }, token).ConfigureAwait(false);
        }

        /// <summary>
        /// Gets bearer token details for the current request context.
        /// </summary>
        /// <param name="token">Cancellation token.</param>
        /// <returns>Token details JSON.</returns>
        public async Task<XenoObjectResponse> GetTokenDetailsAsync(CancellationToken token = default)
        {
            return await GetJsonAsync<XenoObjectResponse>("/v1.0/api/auth/token/details", token).ConfigureAwait(false);
        }

        /// <summary>
        /// Refreshes the active bearer token.
        /// </summary>
        /// <param name="token">Cancellation token.</param>
        /// <returns>New token response JSON.</returns>
        public async Task<TokenResponse> RefreshTokenAsync(CancellationToken token = default)
        {
            return await PostJsonAsync<TokenResponse>("/v1.0/api/auth/token/refresh", new object(), token).ConfigureAwait(false);
        }

        /// <summary>
        /// Logs out the current bearer token.
        /// </summary>
        /// <param name="token">Cancellation token.</param>
        /// <returns>Logout response JSON.</returns>
        public async Task<XenoObjectResponse> LogoutAsync(CancellationToken token = default)
        {
            return await PostJsonAsync<XenoObjectResponse>("/v1.0/api/auth/logout", new object(), token).ConfigureAwait(false);
        }

        /// <summary>
        /// Lists tenants.
        /// </summary>
        /// <param name="token">Cancellation token.</param>
        /// <returns>Tenant array JSON.</returns>
        public async Task<List<Tenant>> ListTenantsAsync(CancellationToken token = default)
        {
            return await GetJsonAsync<List<Tenant>>("/v1.0/api/tenants", token).ConfigureAwait(false);
        }

        /// <summary>
        /// Creates a tenant.
        /// </summary>
        /// <param name="tenant">Tenant payload.</param>
        /// <param name="token">Cancellation token.</param>
        /// <returns>Tenant JSON.</returns>
        public async Task<Tenant> CreateTenantAsync(object tenant, CancellationToken token = default)
        {
            return await PostJsonAsync<Tenant>("/v1.0/api/tenants", tenant ?? new object(), token).ConfigureAwait(false);
        }

        /// <summary>
        /// Updates a tenant.
        /// </summary>
        /// <param name="id">Tenant identifier.</param>
        /// <param name="tenant">Tenant payload.</param>
        /// <param name="token">Cancellation token.</param>
        /// <returns>Tenant JSON.</returns>
        public async Task<Tenant> UpdateTenantAsync(string id, object tenant, CancellationToken token = default)
        {
            return await PutJsonAsync<Tenant>("/v1.0/api/tenants/" + Escape(id), tenant ?? new object(), token).ConfigureAwait(false);
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
        /// Runs a tenant nuke dry run or execution.
        /// </summary>
        /// <param name="request">Tenant nuke request.</param>
        /// <param name="token">Cancellation token.</param>
        /// <returns>Nuke response JSON.</returns>
        public async Task<AdminNukeResponse> NukeTenantAsync(AdminNukeTenantRequest request, CancellationToken token = default)
        {
            return await PostJsonAsync<AdminNukeResponse>("/v1.0/api/admin/nuke/tenant", request ?? new AdminNukeTenantRequest(), token).ConfigureAwait(false);
        }

        /// <summary>
        /// Lists users.
        /// </summary>
        /// <param name="token">Cancellation token.</param>
        /// <returns>User array JSON.</returns>
        public async Task<List<User>> ListUsersAsync(CancellationToken token = default)
        {
            return await GetJsonAsync<List<User>>("/v1.0/api/users", token).ConfigureAwait(false);
        }

        /// <summary>
        /// Creates a user.
        /// </summary>
        /// <param name="user">User payload.</param>
        /// <param name="token">Cancellation token.</param>
        /// <returns>User JSON.</returns>
        public async Task<User> CreateUserAsync(object user, CancellationToken token = default)
        {
            return await PostJsonAsync<User>("/v1.0/api/users", user ?? new object(), token).ConfigureAwait(false);
        }

        /// <summary>
        /// Updates a user.
        /// </summary>
        /// <param name="id">User identifier.</param>
        /// <param name="user">User payload.</param>
        /// <param name="token">Cancellation token.</param>
        /// <returns>User JSON.</returns>
        public async Task<User> UpdateUserAsync(string id, object user, CancellationToken token = default)
        {
            return await PutJsonAsync<User>("/v1.0/api/users/" + Escape(id), user ?? new object(), token).ConfigureAwait(false);
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
        /// Runs a user nuke dry run or execution.
        /// </summary>
        /// <param name="request">User nuke request.</param>
        /// <param name="token">Cancellation token.</param>
        /// <returns>Nuke response JSON.</returns>
        public async Task<AdminNukeResponse> NukeUserAsync(AdminNukeUserRequest request, CancellationToken token = default)
        {
            return await PostJsonAsync<AdminNukeResponse>("/v1.0/api/admin/nuke/user", request ?? new AdminNukeUserRequest(), token).ConfigureAwait(false);
        }

        /// <summary>
        /// Lists credentials.
        /// </summary>
        /// <param name="token">Cancellation token.</param>
        /// <returns>Credential array JSON.</returns>
        public async Task<List<Credential>> ListCredentialsAsync(CancellationToken token = default)
        {
            return await GetJsonAsync<List<Credential>>("/v1.0/api/credentials", token).ConfigureAwait(false);
        }

        /// <summary>
        /// Creates a credential.
        /// </summary>
        /// <param name="credential">Credential payload.</param>
        /// <param name="token">Cancellation token.</param>
        /// <returns>Credential JSON.</returns>
        public async Task<Credential> CreateCredentialAsync(object credential, CancellationToken token = default)
        {
            return await PostJsonAsync<Credential>("/v1.0/api/credentials", credential ?? new object(), token).ConfigureAwait(false);
        }

        /// <summary>
        /// Updates a credential.
        /// </summary>
        /// <param name="id">Credential identifier.</param>
        /// <param name="credential">Credential payload.</param>
        /// <param name="token">Cancellation token.</param>
        /// <returns>Credential JSON.</returns>
        public async Task<Credential> UpdateCredentialAsync(string id, object credential, CancellationToken token = default)
        {
            return await PutJsonAsync<Credential>("/v1.0/api/credentials/" + Escape(id), credential ?? new object(), token).ConfigureAwait(false);
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
        public async Task<List<Role>> ListRolesAsync(CancellationToken token = default)
        {
            return await GetJsonAsync<List<Role>>("/v1.0/api/roles", token).ConfigureAwait(false);
        }

        /// <summary>
        /// Creates a role.
        /// </summary>
        /// <param name="role">Role payload.</param>
        /// <param name="token">Cancellation token.</param>
        /// <returns>Role JSON.</returns>
        public async Task<Role> CreateRoleAsync(object role, CancellationToken token = default)
        {
            return await PostJsonAsync<Role>("/v1.0/api/roles", role ?? new object(), token).ConfigureAwait(false);
        }

        /// <summary>
        /// Updates a role.
        /// </summary>
        /// <param name="id">Role identifier.</param>
        /// <param name="role">Role payload.</param>
        /// <param name="token">Cancellation token.</param>
        /// <returns>Role JSON.</returns>
        public async Task<Role> UpdateRoleAsync(string id, object role, CancellationToken token = default)
        {
            return await PutJsonAsync<Role>("/v1.0/api/roles/" + Escape(id), role ?? new object(), token).ConfigureAwait(false);
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
        public async Task<List<Permission>> ListPermissionsAsync(CancellationToken token = default)
        {
            return await GetJsonAsync<List<Permission>>("/v1.0/api/permissions", token).ConfigureAwait(false);
        }

        /// <summary>
        /// Lists permissions the current caller may grant.
        /// </summary>
        /// <param name="token">Cancellation token.</param>
        /// <returns>Permission array JSON.</returns>
        public async Task<List<Permission>> ListGrantablePermissionsAsync(CancellationToken token = default)
        {
            return await GetJsonAsync<List<Permission>>("/v1.0/api/permissions/grantable", token).ConfigureAwait(false);
        }

        /// <summary>
        /// Creates a permission.
        /// </summary>
        /// <param name="permission">Permission payload.</param>
        /// <param name="token">Cancellation token.</param>
        /// <returns>Permission JSON.</returns>
        public async Task<Permission> CreatePermissionAsync(object permission, CancellationToken token = default)
        {
            return await PostJsonAsync<Permission>("/v1.0/api/permissions", permission ?? new object(), token).ConfigureAwait(false);
        }

        /// <summary>
        /// Updates a permission.
        /// </summary>
        /// <param name="id">Permission identifier.</param>
        /// <param name="permission">Permission payload.</param>
        /// <param name="token">Cancellation token.</param>
        /// <returns>Permission JSON.</returns>
        public async Task<Permission> UpdatePermissionAsync(string id, object permission, CancellationToken token = default)
        {
            return await PutJsonAsync<Permission>("/v1.0/api/permissions/" + Escape(id), permission ?? new object(), token).ConfigureAwait(false);
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
        public async Task<List<EffectivePermission>> ListEffectivePermissionsAsync(CancellationToken token = default)
        {
            return await GetJsonAsync<List<EffectivePermission>>("/v1.0/api/effective-permissions", token).ConfigureAwait(false);
        }

        /// <summary>
        /// Lists role assignments.
        /// </summary>
        /// <param name="token">Cancellation token.</param>
        /// <returns>Role assignment array JSON.</returns>
        public async Task<List<RoleAssignment>> ListRoleAssignmentsAsync(CancellationToken token = default)
        {
            return await GetJsonAsync<List<RoleAssignment>>("/v1.0/api/role-assignments", token).ConfigureAwait(false);
        }

        /// <summary>
        /// Creates a role assignment for a user or credential principal.
        /// </summary>
        /// <param name="assignment">Role assignment payload.</param>
        /// <param name="token">Cancellation token.</param>
        /// <returns>Role assignment JSON.</returns>
        public async Task<RoleAssignment> CreateRoleAssignmentAsync(object assignment, CancellationToken token = default)
        {
            return await PostJsonAsync<RoleAssignment>("/v1.0/api/role-assignments", assignment ?? new object(), token).ConfigureAwait(false);
        }

        /// <summary>
        /// Deletes a role assignment.
        /// </summary>
        /// <param name="id">Role assignment identifier.</param>
        /// <param name="token">Cancellation token.</param>
        /// <returns>Task.</returns>
        public async Task DeleteRoleAssignmentAsync(string id, CancellationToken token = default)
        {
            await DeleteAsync("/v1.0/api/role-assignments/" + Escape(id), token).ConfigureAwait(false);
        }

        /// <summary>
        /// Lists audit records.
        /// </summary>
        /// <param name="token">Cancellation token.</param>
        /// <returns>Audit array JSON.</returns>
        public async Task<List<AuditRecord>> ListAuditRecordsAsync(CancellationToken token = default)
        {
            return await GetJsonAsync<List<AuditRecord>>("/v1.0/api/audit", token).ConfigureAwait(false);
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
        public async Task<List<XenoClientRecord>> ListClientsAsync(CancellationToken token = default)
        {
            return await GetJsonAsync<List<XenoClientRecord>>("/v1.0/api/clients", token).ConfigureAwait(false);
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
        public async Task<XenoObjectResponse> DeleteClientModelAsync(object model, CancellationToken token = default)
        {
            return await DeleteJsonAsync<XenoObjectResponse>("/v1.0/api/client-models", model ?? new object(), token).ConfigureAwait(false);
        }

        /// <summary>
        /// Gets server-collected client telemetry status.
        /// </summary>
        /// <param name="token">Cancellation token.</param>
        /// <returns>Telemetry status JSON.</returns>
        public async Task<ServerClientTelemetryCollectionStatusResponse> GetClientTelemetryStatusAsync(CancellationToken token = default)
        {
            return await GetJsonAsync<ServerClientTelemetryCollectionStatusResponse>("/v1.0/api/client-telemetry/status", token).ConfigureAwait(false);
        }

        /// <summary>
        /// Gets latest server-collected host and provider telemetry.
        /// </summary>
        /// <param name="filters">Optional query-string filters.</param>
        /// <param name="token">Cancellation token.</param>
        /// <returns>Latest telemetry JSON.</returns>
        public async Task<ServerClientTelemetryLatestResponse> GetLatestClientTelemetryAsync(IDictionary<string, string>? filters = null, CancellationToken token = default)
        {
            return await GetJsonAsync<ServerClientTelemetryLatestResponse>("/v1.0/api/client-telemetry/latest" + QueryString(filters), token).ConfigureAwait(false);
        }

        /// <summary>
        /// Triggers server-side telemetry collection.
        /// </summary>
        /// <param name="request">Collection request.</param>
        /// <param name="token">Cancellation token.</param>
        /// <returns>Collection trigger response JSON.</returns>
        public async Task<ServerClientTelemetryCollectResponse> CollectClientTelemetryAsync(object request, CancellationToken token = default)
        {
            return await PostJsonAsync<ServerClientTelemetryCollectResponse>("/v1.0/api/client-telemetry/collect", request ?? new object(), token).ConfigureAwait(false);
        }

        /// <summary>
        /// Triggers immediate provider telemetry collection.
        /// </summary>
        /// <param name="request">Collection request.</param>
        /// <param name="token">Cancellation token.</param>
        /// <returns>Collection response.</returns>
        public async Task<ServerClientTelemetryCollectResponse> CollectClientProviderTelemetryAsync(object request, CancellationToken token = default)
        {
            return await PostJsonAsync<ServerClientTelemetryCollectResponse>("/v1.0/api/client-telemetry/providers/collect", request ?? new object(), token).ConfigureAwait(false);
        }

        /// <summary>
        /// Searches host telemetry history.
        /// </summary>
        /// <param name="request">Search request.</param>
        /// <param name="token">Cancellation token.</param>
        /// <returns>Search result JSON.</returns>
        public async Task<ServerClientTelemetrySearchResult> SearchClientTelemetryAsync(object request, CancellationToken token = default)
        {
            return await PostJsonAsync<ServerClientTelemetrySearchResult>("/v1.0/api/client-telemetry/history/search", request ?? new object(), token).ConfigureAwait(false);
        }

        /// <summary>
        /// Gets host telemetry rollups.
        /// </summary>
        /// <param name="request">Rollup request.</param>
        /// <param name="token">Cancellation token.</param>
        /// <returns>Rollup JSON.</returns>
        public async Task<List<ServerClientTelemetryRollupBucket>> GetClientTelemetryRollupsAsync(object request, CancellationToken token = default)
        {
            return await PostJsonAsync<List<ServerClientTelemetryRollupBucket>>("/v1.0/api/client-telemetry/history/rollups", request ?? new object(), token).ConfigureAwait(false);
        }

        /// <summary>
        /// Gets one host telemetry detail.
        /// </summary>
        /// <param name="id">Sample identifier.</param>
        /// <param name="token">Cancellation token.</param>
        /// <returns>Telemetry detail JSON.</returns>
        public async Task<ServerClientTelemetrySampleDetail> GetClientTelemetryDetailAsync(string id, CancellationToken token = default)
        {
            return await GetJsonAsync<ServerClientTelemetrySampleDetail>("/v1.0/api/client-telemetry/history/" + Escape(id), token).ConfigureAwait(false);
        }

        /// <summary>
        /// Deletes one host telemetry sample.
        /// </summary>
        /// <param name="id">Sample identifier.</param>
        /// <param name="token">Cancellation token.</param>
        /// <returns>Task.</returns>
        public async Task DeleteClientTelemetryAsync(string id, CancellationToken token = default)
        {
            await DeleteAsync("/v1.0/api/client-telemetry/history/" + Escape(id), token).ConfigureAwait(false);
        }

        /// <summary>
        /// Bulk deletes host telemetry samples by filter.
        /// </summary>
        /// <param name="filter">Delete filter.</param>
        /// <param name="token">Cancellation token.</param>
        /// <returns>Delete response.</returns>
        public async Task<XenoObjectResponse> BulkDeleteClientTelemetryAsync(object filter, CancellationToken token = default)
        {
            return await PostJsonAsync<XenoObjectResponse>("/v1.0/api/client-telemetry/history/delete", filter ?? new object(), token).ConfigureAwait(false);
        }

        /// <summary>
        /// Gets latest server-collected provider telemetry.
        /// </summary>
        /// <param name="filters">Optional query-string filters.</param>
        /// <param name="token">Cancellation token.</param>
        /// <returns>Latest provider telemetry JSON.</returns>
        public async Task<List<ServerProviderTelemetrySampleRecord>> GetLatestClientProviderTelemetryAsync(IDictionary<string, string>? filters = null, CancellationToken token = default)
        {
            return await GetJsonAsync<List<ServerProviderTelemetrySampleRecord>>("/v1.0/api/client-telemetry/providers/latest" + QueryString(filters), token).ConfigureAwait(false);
        }

        /// <summary>
        /// Searches provider telemetry history.
        /// </summary>
        /// <param name="request">Search request.</param>
        /// <param name="token">Cancellation token.</param>
        /// <returns>Search result JSON.</returns>
        public async Task<ServerProviderTelemetrySearchResult> SearchClientProviderTelemetryAsync(object request, CancellationToken token = default)
        {
            return await PostJsonAsync<ServerProviderTelemetrySearchResult>("/v1.0/api/client-telemetry/providers/history/search", request ?? new object(), token).ConfigureAwait(false);
        }

        /// <summary>
        /// Gets provider telemetry rollups.
        /// </summary>
        /// <param name="request">Rollup request.</param>
        /// <param name="token">Cancellation token.</param>
        /// <returns>Rollup JSON.</returns>
        public async Task<List<ServerProviderTelemetryRollupBucket>> GetClientProviderTelemetryRollupsAsync(object request, CancellationToken token = default)
        {
            return await PostJsonAsync<List<ServerProviderTelemetryRollupBucket>>("/v1.0/api/client-telemetry/providers/history/rollups", request ?? new object(), token).ConfigureAwait(false);
        }

        /// <summary>
        /// Gets one provider telemetry detail.
        /// </summary>
        /// <param name="id">Sample identifier.</param>
        /// <param name="token">Cancellation token.</param>
        /// <returns>Provider telemetry detail JSON.</returns>
        public async Task<ServerProviderTelemetrySampleDetail> GetClientProviderTelemetryDetailAsync(string id, CancellationToken token = default)
        {
            return await GetJsonAsync<ServerProviderTelemetrySampleDetail>("/v1.0/api/client-telemetry/providers/history/" + Escape(id), token).ConfigureAwait(false);
        }

        /// <summary>
        /// Deletes one provider telemetry sample.
        /// </summary>
        /// <param name="id">Sample identifier.</param>
        /// <param name="token">Cancellation token.</param>
        /// <returns>Task.</returns>
        public async Task DeleteClientProviderTelemetryAsync(string id, CancellationToken token = default)
        {
            await DeleteAsync("/v1.0/api/client-telemetry/providers/history/" + Escape(id), token).ConfigureAwait(false);
        }

        /// <summary>
        /// Bulk deletes provider telemetry samples by filter.
        /// </summary>
        /// <param name="filter">Delete filter.</param>
        /// <param name="token">Cancellation token.</param>
        /// <returns>Delete response.</returns>
        public async Task<XenoObjectResponse> BulkDeleteClientProviderTelemetryAsync(object filter, CancellationToken token = default)
        {
            return await PostJsonAsync<XenoObjectResponse>("/v1.0/api/client-telemetry/providers/history/delete", filter ?? new object(), token).ConfigureAwait(false);
        }

        /// <summary>
        /// Lists recent client telemetry collection attempts.
        /// </summary>
        /// <param name="token">Cancellation token.</param>
        /// <returns>Collection attempt records.</returns>
        public async Task<List<ServerClientTelemetryCollectionAttemptRecord>> ListClientTelemetryCollectionAttemptsAsync(CancellationToken token = default)
        {
            return await GetJsonAsync<List<ServerClientTelemetryCollectionAttemptRecord>>("/v1.0/api/client-telemetry/collection-attempts", token).ConfigureAwait(false);
        }

        /// <summary>
        /// Gets telemetry metric catalog.
        /// </summary>
        /// <param name="token">Cancellation token.</param>
        /// <returns>Metric catalog JSON.</returns>
        public async Task<List<RoutingPolicyMetricDefinition>> GetClientTelemetryMetricCatalogAsync(CancellationToken token = default)
        {
            return await GetJsonAsync<List<RoutingPolicyMetricDefinition>>("/v1.0/api/client-telemetry/metrics", token).ConfigureAwait(false);
        }

        /// <summary>
        /// Lists model definitions.
        /// </summary>
        /// <param name="token">Cancellation token.</param>
        /// <returns>Model definition array JSON.</returns>
        public async Task<List<ModelDefinition>> ListModelDefinitionsAsync(CancellationToken token = default)
        {
            return await GetJsonAsync<List<ModelDefinition>>("/v1.0/api/model-definitions", token).ConfigureAwait(false);
        }

        /// <summary>
        /// Lists model configurations.
        /// </summary>
        /// <param name="token">Cancellation token.</param>
        /// <returns>Model configuration array JSON.</returns>
        public async Task<List<ModelConfiguration>> ListModelConfigurationsAsync(CancellationToken token = default)
        {
            return await GetJsonAsync<List<ModelConfiguration>>("/v1.0/api/model-configurations", token).ConfigureAwait(false);
        }

        /// <summary>
        /// Lists tenant routing policies.
        /// </summary>
        /// <param name="token">Cancellation token.</param>
        /// <returns>Routing policy array JSON.</returns>
        public async Task<List<RoutingPolicy>> ListRoutingPoliciesAsync(CancellationToken token = default)
        {
            return await GetJsonAsync<List<RoutingPolicy>>("/v1.0/api/routing-policies", token).ConfigureAwait(false);
        }

        /// <summary>
        /// Creates a routing policy.
        /// </summary>
        /// <param name="policy">Routing policy payload.</param>
        /// <param name="token">Cancellation token.</param>
        /// <returns>Routing policy JSON.</returns>
        public async Task<RoutingPolicy> CreateRoutingPolicyAsync(object policy, CancellationToken token = default)
        {
            return await PostJsonAsync<RoutingPolicy>("/v1.0/api/routing-policies", policy ?? new object(), token).ConfigureAwait(false);
        }

        /// <summary>
        /// Validates a routing policy without saving it.
        /// </summary>
        /// <param name="policy">Routing policy payload.</param>
        /// <param name="token">Cancellation token.</param>
        /// <returns>Validation result.</returns>
        public async Task<RoutingPolicyValidationResult> ValidateRoutingPolicyAsync(object policy, CancellationToken token = default)
        {
            return await PostJsonAsync<RoutingPolicyValidationResult>("/v1.0/api/routing-policies/validate", policy ?? new object(), token).ConfigureAwait(false);
        }

        /// <summary>
        /// Updates a routing policy.
        /// </summary>
        /// <param name="id">Routing policy identifier.</param>
        /// <param name="policy">Routing policy payload.</param>
        /// <param name="token">Cancellation token.</param>
        /// <returns>Routing policy JSON.</returns>
        public async Task<RoutingPolicy> UpdateRoutingPolicyAsync(string id, object policy, CancellationToken token = default)
        {
            return await PutJsonAsync<RoutingPolicy>("/v1.0/api/routing-policies/" + Escape(id), policy ?? new object(), token).ConfigureAwait(false);
        }

        /// <summary>
        /// Deletes a non-default routing policy.
        /// </summary>
        /// <param name="id">Routing policy identifier.</param>
        /// <param name="token">Cancellation token.</param>
        /// <returns>Task.</returns>
        public async Task DeleteRoutingPolicyAsync(string id, CancellationToken token = default)
        {
            await DeleteAsync("/v1.0/api/routing-policies/" + Escape(id), token).ConfigureAwait(false);
        }

        /// <summary>
        /// Lists OpenAI-compatible models.
        /// </summary>
        /// <param name="token">Cancellation token.</param>
        /// <returns>OpenAI model list JSON.</returns>
        public async Task<OpenAiModelList> ListOpenAiModelsAsync(CancellationToken token = default)
        {
            return await GetJsonAsync<OpenAiModelList>("/v1/models", token).ConfigureAwait(false);
        }

        /// <summary>
        /// Creates an OpenAI-compatible chat completion.
        /// </summary>
        /// <param name="request">Chat completion payload.</param>
        /// <param name="token">Cancellation token.</param>
        /// <returns>Completion response JSON.</returns>
        public async Task<OpenAiChatCompletion> CreateChatCompletionAsync(object request, CancellationToken token = default)
        {
            return await PostJsonAsync<OpenAiChatCompletion>("/v1/chat/completions", request ?? new object(), token).ConfigureAwait(false);
        }

        /// <summary>
        /// Creates an OpenAI-compatible completion.
        /// </summary>
        /// <param name="request">Completion payload.</param>
        /// <param name="token">Cancellation token.</param>
        /// <returns>Completion response JSON.</returns>
        public async Task<OpenAiChatCompletion> CreateCompletionAsync(object request, CancellationToken token = default)
        {
            return await PostJsonAsync<OpenAiChatCompletion>("/v1/completions", request ?? new object(), token).ConfigureAwait(false);
        }

        /// <summary>
        /// Creates OpenAI-compatible embeddings.
        /// </summary>
        /// <param name="request">Embedding payload.</param>
        /// <param name="token">Cancellation token.</param>
        /// <returns>Embedding response JSON.</returns>
        public async Task<OpenAiEmbeddingList> CreateEmbeddingAsync(object request, CancellationToken token = default)
        {
            return await PostJsonAsync<OpenAiEmbeddingList>("/v1/embeddings", request ?? new object(), token).ConfigureAwait(false);
        }

        /// <summary>
        /// Lists Ollama-compatible models.
        /// </summary>
        /// <param name="token">Cancellation token.</param>
        /// <returns>Ollama model list JSON.</returns>
        public async Task<XenoObjectResponse> ListOllamaModelsAsync(CancellationToken token = default)
        {
            return await GetJsonAsync<XenoObjectResponse>("/api/tags", token).ConfigureAwait(false);
        }

        /// <summary>
        /// Creates an Ollama-compatible chat completion.
        /// </summary>
        /// <param name="request">Ollama chat payload.</param>
        /// <param name="token">Cancellation token.</param>
        /// <returns>Ollama chat response JSON.</returns>
        public async Task<XenoObjectResponse> CreateOllamaChatAsync(object request, CancellationToken token = default)
        {
            return await PostJsonAsync<XenoObjectResponse>("/api/chat", request ?? new object(), token).ConfigureAwait(false);
        }

        /// <summary>
        /// Creates an Ollama-compatible generation.
        /// </summary>
        /// <param name="request">Ollama generate payload.</param>
        /// <param name="token">Cancellation token.</param>
        /// <returns>Ollama generation response JSON.</returns>
        public async Task<XenoObjectResponse> CreateOllamaGenerateAsync(object request, CancellationToken token = default)
        {
            return await PostJsonAsync<XenoObjectResponse>("/api/generate", request ?? new object(), token).ConfigureAwait(false);
        }

        /// <summary>
        /// Creates Ollama-compatible embeddings.
        /// </summary>
        /// <param name="request">Ollama embedding payload.</param>
        /// <param name="token">Cancellation token.</param>
        /// <returns>Ollama embedding response JSON.</returns>
        public async Task<XenoObjectResponse> CreateOllamaEmbeddingAsync(object request, CancellationToken token = default)
        {
            return await PostJsonAsync<XenoObjectResponse>("/api/embed", request ?? new object(), token).ConfigureAwait(false);
        }

        /// <summary>
        /// Lists request history.
        /// </summary>
        /// <param name="filters">Optional query-string filters.</param>
        /// <param name="token">Cancellation token.</param>
        /// <returns>Paged request history result JSON.</returns>
        public async Task<RequestHistorySearchResult> ListRequestHistoryAsync(IDictionary<string, string>? filters = null, CancellationToken token = default)
        {
            return await GetJsonAsync<RequestHistorySearchResult>("/v1.0/api/request-history" + QueryString(filters), token).ConfigureAwait(false);
        }

        /// <summary>
        /// Gets a public model token leaderboard from request history.
        /// </summary>
        /// <param name="filters">Optional query-string filters.</param>
        /// <param name="token">Cancellation token.</param>
        /// <returns>Ranked model token usage response.</returns>
        public async Task<ModelTokenLeaderboardResponse> GetModelTokenLeaderboardAsync(IDictionary<string, string>? filters = null, CancellationToken token = default)
        {
            return await GetJsonAsync<ModelTokenLeaderboardResponse>("/v1.0/api/request-history/model-leaderboard" + QueryString(filters), token).ConfigureAwait(false);
        }

        /// <summary>
        /// Queries the public model token leaderboard from request history.
        /// </summary>
        /// <param name="request">Leaderboard request body.</param>
        /// <param name="token">Cancellation token.</param>
        /// <returns>Ranked model token usage response.</returns>
        public async Task<ModelTokenLeaderboardResponse> QueryModelTokenLeaderboardAsync(object request, CancellationToken token = default)
        {
            return await PostJsonAsync<ModelTokenLeaderboardResponse>("/v1.0/api/request-history/model-leaderboard", request ?? new object(), token).ConfigureAwait(false);
        }

        /// <summary>
        /// Gets a request history entry.
        /// </summary>
        /// <param name="id">Request history identifier.</param>
        /// <param name="token">Cancellation token.</param>
        /// <returns>Request history entry JSON.</returns>
        public async Task<RequestHistoryEntry> GetRequestHistoryAsync(string id, CancellationToken token = default)
        {
            return await GetJsonAsync<RequestHistoryEntry>("/v1.0/api/request-history/" + Escape(id), token).ConfigureAwait(false);
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
        public async Task<RequestHistorySummary> GetRequestHistorySummaryAsync(IDictionary<string, string>? filters = null, CancellationToken token = default)
        {
            return await GetJsonAsync<RequestHistorySummary>("/v1.0/api/request-history/summary" + QueryString(filters), token).ConfigureAwait(false);
        }

        /// <summary>
        /// Gets request analytics overview.
        /// </summary>
        /// <param name="token">Cancellation token.</param>
        /// <returns>Overview JSON.</returns>
        public async Task<XenoObjectResponse> GetRequestAnalyticsOverviewAsync(CancellationToken token = default)
        {
            return await GetJsonAsync<XenoObjectResponse>("/v1.0/api/request-history/analytics/overview", token).ConfigureAwait(false);
        }

        /// <summary>
        /// Bulk deletes request history.
        /// </summary>
        /// <param name="filter">Delete filter.</param>
        /// <param name="token">Cancellation token.</param>
        /// <returns>Delete response JSON.</returns>
        public async Task<XenoObjectResponse> BulkDeleteRequestHistoryAsync(object filter, CancellationToken token = default)
        {
            return await PostJsonAsync<XenoObjectResponse>("/v1.0/api/request-history/bulk", filter ?? new object(), token).ConfigureAwait(false);
        }

        /// <summary>
        /// Gets analytics overview.
        /// </summary>
        /// <param name="token">Cancellation token.</param>
        /// <returns>Overview JSON.</returns>
        public async Task<XenoObjectResponse> GetAnalyticsOverviewAsync(CancellationToken token = default)
        {
            return await GetJsonAsync<XenoObjectResponse>("/v1.0/api/analytics/overview", token).ConfigureAwait(false);
        }

        /// <summary>
        /// Gets server settings.
        /// </summary>
        /// <param name="token">Cancellation token.</param>
        /// <returns>Settings JSON.</returns>
        public async Task<XenoObjectResponse> GetSettingsAsync(CancellationToken token = default)
        {
            return await GetJsonAsync<XenoObjectResponse>("/v1.0/api/settings", token).ConfigureAwait(false);
        }

        /// <summary>
        /// Updates server settings.
        /// </summary>
        /// <param name="settings">Settings payload.</param>
        /// <param name="token">Cancellation token.</param>
        /// <returns>Settings JSON.</returns>
        public async Task<XenoObjectResponse> UpdateSettingsAsync(object settings, CancellationToken token = default)
        {
            return await PostJsonAsync<XenoObjectResponse>("/v1.0/api/settings", settings ?? new object(), token).ConfigureAwait(false);
        }

        /// <summary>
        /// Sends an administrative test email through the configured server email provider.
        /// </summary>
        /// <param name="request">Test email request.</param>
        /// <param name="token">Cancellation token.</param>
        /// <returns>Test email response JSON.</returns>
        public async Task<EmailTestResponse> SendTestEmailAsync(EmailTestRequest request, CancellationToken token = default)
        {
            return await PostJsonAsync<EmailTestResponse>("/v1.0/api/email/test", request ?? new EmailTestRequest(), token).ConfigureAwait(false);
        }

        /// <summary>
        /// Gets platform backup.
        /// </summary>
        /// <param name="token">Cancellation token.</param>
        /// <returns>Backup JSON.</returns>
        public async Task<XenoObjectResponse> GetBackupAsync(CancellationToken token = default)
        {
            return await GetJsonAsync<XenoObjectResponse>("/v1.0/api/backup", token).ConfigureAwait(false);
        }

        /// <summary>
        /// Restores platform backup.
        /// </summary>
        /// <param name="backup">Backup payload.</param>
        /// <param name="token">Cancellation token.</param>
        /// <returns>Restore response JSON.</returns>
        public async Task<XenoObjectResponse> RestoreBackupAsync(object backup, CancellationToken token = default)
        {
            return await PostJsonAsync<XenoObjectResponse>("/v1.0/api/restore", backup ?? new object(), token).ConfigureAwait(false);
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

        private async Task<T> GetJsonAsync<T>(string endpoint, CancellationToken token)
        {
            string json = await SendTextAsync(HttpMethod.Get, endpoint, null, token).ConfigureAwait(false);
            return DeserializeResponse<T>(json, endpoint);
        }

        private async Task<T> PostJsonAsync<T>(string endpoint, object body, CancellationToken token)
        {
            string json = await SendTextAsync(HttpMethod.Post, endpoint, body, token).ConfigureAwait(false);
            return DeserializeResponse<T>(json, endpoint);
        }

        private async Task<T> PutJsonAsync<T>(string endpoint, object body, CancellationToken token)
        {
            string json = await SendTextAsync(HttpMethod.Put, endpoint, body, token).ConfigureAwait(false);
            return DeserializeResponse<T>(json, endpoint);
        }

        private async Task<T> DeleteJsonAsync<T>(string endpoint, object body, CancellationToken token)
        {
            string json = await SendTextAsync(HttpMethod.Delete, endpoint, body, token).ConfigureAwait(false);
            return DeserializeResponse<T>(json, endpoint);
        }

        private async Task DeleteAsync(string endpoint, CancellationToken token)
        {
            await SendTextAsync(HttpMethod.Delete, endpoint, null, token).ConfigureAwait(false);
        }

        private async Task<string> SendTextAsync(HttpMethod method, string endpoint, object? body, CancellationToken token)
        {
            token.ThrowIfCancellationRequested();
            using (HttpRequestMessage request = new HttpRequestMessage(method, endpoint.TrimStart('/')))
            {
                if (body != null)
                {
                    string json = XenoSdkJson.Serialize(body);
                    request.Content = new StringContent(json, Encoding.UTF8, "application/json");
                }

                using (HttpResponseMessage response = await _HttpClient.SendAsync(request, token).ConfigureAwait(false))
                {
                    string responseBody = response.Content == null
                        ? String.Empty
                        : await response.Content.ReadAsStringAsync(token).ConfigureAwait(false);

                    if (!response.IsSuccessStatusCode)
                    {
                        throw new XenoApiException(ExtractErrorMessage(responseBody, response.StatusCode.ToString()), response.StatusCode, endpoint, responseBody);
                    }

                    return String.IsNullOrWhiteSpace(responseBody) ? "{}" : responseBody;
                }
            }
        }

        private static T DeserializeResponse<T>(string json, string endpoint)
        {
            if (typeof(T) == typeof(XenoObjectResponse))
            {
                return (T)(object)XenoObjectResponse.FromJson(json);
            }

            try
            {
                return XenoSdkJson.Deserialize<T>(json);
            }
            catch (JsonException ex)
            {
                throw new XenoApiException("Xeno API response from " + endpoint + " could not be deserialized as " + typeof(T).Name + ": " + ex.Message, System.Net.HttpStatusCode.OK, endpoint, json);
            }
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
            if (!XenoSdkJson.TryDeserialize<XenoErrorResponse>(body, out XenoErrorResponse? error)) return body;
            if (!String.IsNullOrWhiteSpace(error?.Message)) return error.Message;
            if (!String.IsNullOrWhiteSpace(error?.Error)) return error.Error;

            return fallback;
        }
    }
}
