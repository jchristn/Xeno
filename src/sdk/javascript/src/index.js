export class XenoApiError extends Error {
  constructor(message, status, endpoint, response) {
    super(message);
    this.name = 'XenoApiError';
    this.status = status;
    this.endpoint = endpoint;
    this.response = response;
  }
}

export class XenoClient {
  constructor({
    baseUrl = 'http://localhost:9000',
    bearerToken = 'default',
    accessKey = null,
    tenantId = null,
    fetchImpl = globalThis.fetch
  } = {}) {
    if (!baseUrl) {
      throw new Error('baseUrl is required');
    }

    if (typeof fetchImpl !== 'function') {
      throw new Error('fetchImpl must be a function');
    }

    this.baseUrl = baseUrl.replace(/\/+$/, '');
    this.bearerToken = bearerToken;
    this.accessKey = accessKey;
    this.tenantId = tenantId;
    this.fetchImpl = fetchImpl;
  }

  async getHealth() {
    return this.#request('GET', '/health');
  }

  async getOpenApi() {
    return this.#request('GET', '/openapi.json');
  }

  async getMetrics() {
    return this.#request('GET', '/metrics', null, { responseType: 'text' });
  }

  async login(username, password, tenantId = this.tenantId) {
    return this.#request('POST', '/v1.0/api/auth/login', { username, password, tenantId });
  }

  async onboard(request = {}) {
    return this.#request('POST', '/v1.0/api/onboarding/signup', request || {});
  }

  async createToken(accessKey) {
    return this.#request('POST', '/v1.0/api/auth/token', { accessKey });
  }

  async getTokenDetails() {
    return this.#request('GET', '/v1.0/api/auth/token/details');
  }

  async refreshToken() {
    return this.#request('POST', '/v1.0/api/auth/token/refresh', {});
  }

  async logout() {
    return this.#request('POST', '/v1.0/api/auth/logout', {});
  }

  async listTenants() {
    return this.#request('GET', '/v1.0/api/tenants');
  }

  async createTenant(tenant = {}) {
    return this.#request('POST', '/v1.0/api/tenants', tenant);
  }

  async updateTenant(id, tenant = {}) {
    return this.#request('PUT', `/v1.0/api/tenants/${encodeURIComponent(id)}`, tenant);
  }

  async deleteTenant(id) {
    return this.#request('DELETE', `/v1.0/api/tenants/${encodeURIComponent(id)}`);
  }

  async nukeTenant(request = {}) {
    return this.#request('POST', '/v1.0/api/admin/nuke/tenant', request || {});
  }

  async listUsers() {
    return this.#request('GET', '/v1.0/api/users');
  }

  async createUser(user = {}) {
    return this.#request('POST', '/v1.0/api/users', user);
  }

  async updateUser(id, user = {}) {
    return this.#request('PUT', `/v1.0/api/users/${encodeURIComponent(id)}`, user);
  }

  async deleteUser(id) {
    return this.#request('DELETE', `/v1.0/api/users/${encodeURIComponent(id)}`);
  }

  async nukeUser(request = {}) {
    return this.#request('POST', '/v1.0/api/admin/nuke/user', request || {});
  }

  async listCredentials() {
    return this.#request('GET', '/v1.0/api/credentials');
  }

  async createCredential(credential = {}) {
    return this.#request('POST', '/v1.0/api/credentials', credential);
  }

  async updateCredential(id, credential = {}) {
    return this.#request('PUT', `/v1.0/api/credentials/${encodeURIComponent(id)}`, credential);
  }

  async deleteCredential(id) {
    return this.#request('DELETE', `/v1.0/api/credentials/${encodeURIComponent(id)}`);
  }

  async listRoles() {
    return this.#request('GET', '/v1.0/api/roles');
  }

  async createRole(role = {}) {
    return this.#request('POST', '/v1.0/api/roles', role);
  }

  async updateRole(id, role = {}) {
    return this.#request('PUT', `/v1.0/api/roles/${encodeURIComponent(id)}`, role);
  }

  async deleteRole(id) {
    return this.#request('DELETE', `/v1.0/api/roles/${encodeURIComponent(id)}`);
  }

  async listPermissions() {
    return this.#request('GET', '/v1.0/api/permissions');
  }

  async listGrantablePermissions() {
    return this.#request('GET', '/v1.0/api/permissions/grantable');
  }

  async createPermission(permission = {}) {
    return this.#request('POST', '/v1.0/api/permissions', permission);
  }

  async updatePermission(id, permission = {}) {
    return this.#request('PUT', `/v1.0/api/permissions/${encodeURIComponent(id)}`, permission);
  }

  async deletePermission(id) {
    return this.#request('DELETE', `/v1.0/api/permissions/${encodeURIComponent(id)}`);
  }

  async listEffectivePermissions() {
    return this.#request('GET', '/v1.0/api/effective-permissions');
  }

  async listRoleAssignments() {
    return this.#request('GET', '/v1.0/api/role-assignments');
  }

  async createRoleAssignment(assignment = {}) {
    return this.#request('POST', '/v1.0/api/role-assignments', assignment);
  }

  async deleteRoleAssignment(id) {
    return this.#request('DELETE', `/v1.0/api/role-assignments/${encodeURIComponent(id)}`);
  }

  async listAuditRecords() {
    return this.#request('GET', '/v1.0/api/audit');
  }

  async deleteAuditRecord(id) {
    return this.#request('DELETE', `/v1.0/api/audit/${encodeURIComponent(id)}`);
  }

  async listClients() {
    return this.#request('GET', '/v1.0/api/clients');
  }

  async deleteClient(id) {
    return this.#request('DELETE', `/v1.0/api/clients/${encodeURIComponent(id)}`);
  }

  async deleteClientModel(model = {}) {
    return this.#request('DELETE', '/v1.0/api/client-models', model);
  }

  async getClientTelemetryStatus() {
    return this.#request('GET', '/v1.0/api/client-telemetry/status');
  }

  async getLatestClientTelemetry(filters = {}) {
    return this.#request('GET', `/v1.0/api/client-telemetry/latest${this.#queryString(filters)}`);
  }

  async collectClientTelemetry(request = {}) {
    return this.#request('POST', '/v1.0/api/client-telemetry/collect', request);
  }

  async searchClientTelemetry(request = {}) {
    return this.#request('POST', '/v1.0/api/client-telemetry/history/search', request);
  }

  async getClientTelemetryRollups(request = {}) {
    return this.#request('POST', '/v1.0/api/client-telemetry/history/rollups', request);
  }

  async getClientTelemetryDetail(id) {
    return this.#request('GET', `/v1.0/api/client-telemetry/history/${encodeURIComponent(id)}`);
  }

  async deleteClientTelemetry(id) {
    return this.#request('DELETE', `/v1.0/api/client-telemetry/history/${encodeURIComponent(id)}`);
  }

  async getLatestClientProviderTelemetry(filters = {}) {
    return this.#request('GET', `/v1.0/api/client-telemetry/providers/latest${this.#queryString(filters)}`);
  }

  async searchClientProviderTelemetry(request = {}) {
    return this.#request('POST', '/v1.0/api/client-telemetry/providers/history/search', request);
  }

  async getClientProviderTelemetryRollups(request = {}) {
    return this.#request('POST', '/v1.0/api/client-telemetry/providers/history/rollups', request);
  }

  async getClientProviderTelemetryDetail(id) {
    return this.#request('GET', `/v1.0/api/client-telemetry/providers/history/${encodeURIComponent(id)}`);
  }

  async deleteClientProviderTelemetry(id) {
    return this.#request('DELETE', `/v1.0/api/client-telemetry/providers/history/${encodeURIComponent(id)}`);
  }

  async getClientTelemetryMetricCatalog() {
    return this.#request('GET', '/v1.0/api/client-telemetry/metrics');
  }

  async listModelDefinitions() {
    return this.#request('GET', '/v1.0/api/model-definitions');
  }

  async listModelConfigurations() {
    return this.#request('GET', '/v1.0/api/model-configurations');
  }

  async listRoutingPolicies() {
    return this.#request('GET', '/v1.0/api/routing-policies');
  }

  async createRoutingPolicy(policy = {}) {
    return this.#request('POST', '/v1.0/api/routing-policies', policy);
  }

  async updateRoutingPolicy(id, policy = {}) {
    return this.#request('PUT', `/v1.0/api/routing-policies/${encodeURIComponent(id)}`, policy);
  }

  async deleteRoutingPolicy(id) {
    return this.#request('DELETE', `/v1.0/api/routing-policies/${encodeURIComponent(id)}`);
  }

  async listOpenAiModels() {
    return this.#request('GET', '/v1/models');
  }

  async createChatCompletion(request = {}) {
    return this.#request('POST', '/v1/chat/completions', request);
  }

  async createCompletion(request = {}) {
    return this.#request('POST', '/v1/completions', request);
  }

  async createEmbedding(request = {}) {
    return this.#request('POST', '/v1/embeddings', request);
  }

  async listOllamaModels() {
    return this.#request('GET', '/api/tags');
  }

  async createOllamaChat(request = {}) {
    return this.#request('POST', '/api/chat', request);
  }

  async createOllamaGenerate(request = {}) {
    return this.#request('POST', '/api/generate', request);
  }

  async createOllamaEmbedding(request = {}) {
    return this.#request('POST', '/api/embed', request);
  }

  async listRequestHistory(filters = {}) {
    return this.#request('GET', `/v1.0/api/request-history${this.#queryString(filters)}`);
  }

  async getModelTokenLeaderboard(filters = {}) {
    return this.#request('GET', `/v1.0/api/request-history/model-leaderboard${this.#queryString(filters)}`);
  }

  async queryModelTokenLeaderboard(request = {}) {
    return this.#request('POST', '/v1.0/api/request-history/model-leaderboard', request ?? {});
  }

  async getRequestHistory(id) {
    return this.#request('GET', `/v1.0/api/request-history/${encodeURIComponent(id)}`);
  }

  async deleteRequestHistory(id) {
    return this.#request('DELETE', `/v1.0/api/request-history/${encodeURIComponent(id)}`);
  }

  async getRequestHistorySummary(filters = {}) {
    return this.#request('GET', `/v1.0/api/request-history/summary${this.#queryString(filters)}`);
  }

  async getRequestAnalyticsOverview() {
    return this.#request('GET', '/v1.0/api/request-history/analytics/overview');
  }

  async bulkDeleteRequestHistory(filter = {}) {
    return this.#request('POST', '/v1.0/api/request-history/bulk', filter);
  }

  async getAnalyticsOverview() {
    return this.#request('GET', '/v1.0/api/analytics/overview');
  }

  async getSettings() {
    return this.#request('GET', '/v1.0/api/settings');
  }

  async updateSettings(settings = {}) {
    return this.#request('POST', '/v1.0/api/settings', settings);
  }

  async sendTestEmail(request = {}) {
    return this.#request('POST', '/v1.0/api/email/test', request || {});
  }

  async getBackup() {
    return this.#request('GET', '/v1.0/api/backup');
  }

  async restoreBackup(backup = {}) {
    return this.#request('POST', '/v1.0/api/restore', backup);
  }

  async #request(method, endpoint, body = null, options = {}) {
    const headers = {};
    if (options.responseType !== 'text') {
      headers['Content-Type'] = 'application/json';
    }

    if (this.bearerToken) {
      headers.Authorization = `Bearer ${this.bearerToken}`;
    }

    if (this.accessKey) {
      headers['x-access-key'] = this.accessKey;
    }

    if (this.tenantId) {
      headers['x-tenant-guid'] = this.tenantId;
    }

    const response = await this.fetchImpl(`${this.baseUrl}${endpoint}`, {
      method,
      headers,
      body: body != null ? JSON.stringify(body) : null
    });

    if (!response.ok) {
      const errorData = await response.json().catch(() => ({}));
      throw new XenoApiError(
        errorData?.message || errorData?.error || `HTTP ${response.status}`,
        response.status,
        endpoint,
        errorData
      );
    }

    if (response.status === 204) {
      return null;
    }

    if (options.responseType === 'text') {
      return response.text();
    }

    return response.json();
  }

  #queryString(filters = {}) {
    const query = new URLSearchParams();
    for (const [key, value] of Object.entries(filters || {})) {
      if (value !== null && value !== undefined && value !== '') {
        query.append(key, value);
      }
    }

    const serialized = query.toString();
    return serialized ? `?${serialized}` : '';
  }
}
