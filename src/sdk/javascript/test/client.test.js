import assert from 'node:assert/strict';
import test from 'node:test';
import { XenoApiError, XenoClient } from '../src/index.js';

function jsonResponse(data = {}, status = 200) {
  return {
    ok: status >= 200 && status < 300,
    status,
    async json() {
      return data;
    },
    async text() {
      return typeof data === 'string' ? data : JSON.stringify(data);
    }
  };
}

function createFetch(responseFactory = () => jsonResponse({})) {
  const calls = [];
  const fetchImpl = async (url, options) => {
    calls.push({ url, options });
    return responseFactory(url, options);
  };
  fetchImpl.calls = calls;
  return fetchImpl;
}

test('listTenants sends tenant and bearer headers', async () => {
  const fetchImpl = createFetch(() => jsonResponse([]));
  const client = new XenoClient({
    baseUrl: 'https://xeno.local/',
    bearerToken: 'token-123',
    tenantId: 'ten_123',
    fetchImpl
  });

  await client.listTenants();

  assert.equal(fetchImpl.calls[0].url, 'https://xeno.local/v1.0/api/tenants');
  assert.equal(fetchImpl.calls[0].options.method, 'GET');
  assert.equal(fetchImpl.calls[0].options.headers.Authorization, 'Bearer token-123');
  assert.equal(fetchImpl.calls[0].options.headers['x-tenant-guid'], 'ten_123');
});

test('default options send seeded bearer without tenant header', async () => {
  const fetchImpl = createFetch();
  const client = new XenoClient({
    baseUrl: 'https://xeno.local',
    fetchImpl
  });

  await client.listOpenAiModels();

  assert.equal(fetchImpl.calls[0].options.headers.Authorization, 'Bearer default');
  assert.equal(fetchImpl.calls[0].options.headers['x-tenant-guid'], undefined);
});

test('management CRUD methods use expected routes and bodies', async () => {
  const fetchImpl = createFetch((url, options) => jsonResponse({}, options.method === 'DELETE' ? 204 : 200));
  const client = new XenoClient({ baseUrl: 'https://xeno.local', fetchImpl });

  await client.createTenant({ name: 'Tenant' });
  await client.updateTenant('ten_1', { name: 'Tenant 2' });
  await client.deleteTenant('ten_1');
  await client.createUser({ username: 'user@xeno', password: 'password' });
  await client.updateUser('usr_1', { active: false });
  await client.deleteUser('usr_1');
  await client.createCredential({ userId: 'usr_1' });
  await client.updateCredential('cred_1', { active: true });
  await client.deleteCredential('cred_1');
  await client.createRole({ name: 'Operator' });
  await client.updateRole('role_1', { permissionIds: ['perm_1'] });
  await client.deleteRole('role_1');
  await client.listGrantablePermissions();
  await client.createPermission({ resourceType: 'Clients', operationType: 'Read' });
  await client.updatePermission('perm_1', { permit: false });
  await client.deletePermission('perm_1');
  await client.listRoleAssignments();
  await client.createRoleAssignment({ roleId: 'role_1', principalType: 'User', principalId: 'usr_1' });
  await client.deleteRoleAssignment('rassn_1');
  await client.createRoutingPolicy({ name: 'Policy', modelName: '*', clientName: '*' });
  await client.updateRoutingPolicy('rpol_1', { loadBalancingMode: 'Random' });
  await client.deleteRoutingPolicy('rpol_1');

  assert.deepEqual(fetchImpl.calls.map((call) => [call.options.method, call.url]), [
    ['POST', 'https://xeno.local/v1.0/api/tenants'],
    ['PUT', 'https://xeno.local/v1.0/api/tenants/ten_1'],
    ['DELETE', 'https://xeno.local/v1.0/api/tenants/ten_1'],
    ['POST', 'https://xeno.local/v1.0/api/users'],
    ['PUT', 'https://xeno.local/v1.0/api/users/usr_1'],
    ['DELETE', 'https://xeno.local/v1.0/api/users/usr_1'],
    ['POST', 'https://xeno.local/v1.0/api/credentials'],
    ['PUT', 'https://xeno.local/v1.0/api/credentials/cred_1'],
    ['DELETE', 'https://xeno.local/v1.0/api/credentials/cred_1'],
    ['POST', 'https://xeno.local/v1.0/api/roles'],
    ['PUT', 'https://xeno.local/v1.0/api/roles/role_1'],
    ['DELETE', 'https://xeno.local/v1.0/api/roles/role_1'],
    ['GET', 'https://xeno.local/v1.0/api/permissions/grantable'],
    ['POST', 'https://xeno.local/v1.0/api/permissions'],
    ['PUT', 'https://xeno.local/v1.0/api/permissions/perm_1'],
    ['DELETE', 'https://xeno.local/v1.0/api/permissions/perm_1'],
    ['GET', 'https://xeno.local/v1.0/api/role-assignments'],
    ['POST', 'https://xeno.local/v1.0/api/role-assignments'],
    ['DELETE', 'https://xeno.local/v1.0/api/role-assignments/rassn_1'],
    ['POST', 'https://xeno.local/v1.0/api/routing-policies'],
    ['PUT', 'https://xeno.local/v1.0/api/routing-policies/rpol_1'],
    ['DELETE', 'https://xeno.local/v1.0/api/routing-policies/rpol_1']
  ]);
  assert.match(fetchImpl.calls[0].options.body, /"name":"Tenant"/);
  assert.match(fetchImpl.calls[10].options.body, /"permissionIds":\["perm_1"\]/);
  assert.match(fetchImpl.calls[17].options.body, /"principalId":"usr_1"/);
  assert.match(fetchImpl.calls[19].options.body, /"modelName":"\*"/);
});

test('OpenAI and request history methods use expected routes', async () => {
  const fetchImpl = createFetch();
  const client = new XenoClient({ baseUrl: 'https://xeno.local', fetchImpl });

  await client.listOpenAiModels();
  await client.createChatCompletion({ model: 'llama3.2:latest' });
  await client.createEmbedding({ model: 'text-embedding-3-small', input: 'hello' });
  await client.listOllamaModels();
  await client.createOllamaChat({ model: 'llama3.2:latest' });
  await client.createOllamaGenerate({ model: 'llama3.2:latest', prompt: 'hello' });
  await client.createOllamaEmbedding({ model: 'llama3.2:latest', input: 'hello' });
  await client.listRequestHistory({ model: 'llama3.2:latest', empty: '' });
  await client.getModelTokenLeaderboard({ limit: 10, offset: 0, empty: '' });
  await client.queryModelTokenLeaderboard({ limit: 10, offset: 0 });
  await client.getModelTokenLeaderboard();
  await client.queryModelTokenLeaderboard();
  await client.queryModelTokenLeaderboard(null);
  await client.getRequestHistorySummary({ bucketMinutes: 60 });

  assert.deepEqual(fetchImpl.calls.map((call) => [call.options.method, call.url]), [
    ['GET', 'https://xeno.local/v1/models'],
    ['POST', 'https://xeno.local/v1/chat/completions'],
    ['POST', 'https://xeno.local/v1/embeddings'],
    ['GET', 'https://xeno.local/api/tags'],
    ['POST', 'https://xeno.local/api/chat'],
    ['POST', 'https://xeno.local/api/generate'],
    ['POST', 'https://xeno.local/api/embed'],
    ['GET', 'https://xeno.local/v1.0/api/request-history?model=llama3.2%3Alatest'],
    ['GET', 'https://xeno.local/v1.0/api/request-history/model-leaderboard?limit=10&offset=0'],
    ['POST', 'https://xeno.local/v1.0/api/request-history/model-leaderboard'],
    ['GET', 'https://xeno.local/v1.0/api/request-history/model-leaderboard'],
    ['POST', 'https://xeno.local/v1.0/api/request-history/model-leaderboard'],
    ['POST', 'https://xeno.local/v1.0/api/request-history/model-leaderboard'],
    ['GET', 'https://xeno.local/v1.0/api/request-history/summary?bucketMinutes=60']
  ]);
  assert.match(fetchImpl.calls[9].options.body, /"offset":0/);
  assert.equal(fetchImpl.calls[11].options.body, '{}');
  assert.equal(fetchImpl.calls[12].options.body, '{}');
});

test('credential options send tenant and access key headers', async () => {
  const fetchImpl = createFetch();
  const client = new XenoClient({
    baseUrl: 'https://xeno.local',
    bearerToken: null,
    accessKey: 'xeno_access',
    tenantId: 'ten_123',
    fetchImpl
  });

  await client.listOpenAiModels();

  assert.equal(fetchImpl.calls[0].options.headers['x-access-key'], 'xeno_access');
  assert.equal(fetchImpl.calls[0].options.headers['x-secret-key'], undefined);
  assert.equal(fetchImpl.calls[0].options.headers['x-tenant-guid'], 'ten_123');
});

test('OpenAI errors surface authentication and authorization status', async () => {
  const anonymousFetch = createFetch(() => jsonResponse({ error: 'Unauthenticated', message: 'Authentication is required.' }, 401));
  const anonymousClient = new XenoClient({ baseUrl: 'https://xeno.local', bearerToken: null, fetchImpl: anonymousFetch });
  await assert.rejects(() => anonymousClient.listOpenAiModels(), (error) => {
    assert.ok(error instanceof XenoApiError);
    assert.equal(error.status, 401);
    assert.equal(error.endpoint, '/v1/models');
    return true;
  });

  const deniedFetch = createFetch(() => jsonResponse({ error: 'PermissionDenied', message: 'Permission denied.' }, 403));
  const deniedClient = new XenoClient({ baseUrl: 'https://xeno.local', bearerToken: 'token-123', tenantId: 'ten_123', fetchImpl: deniedFetch });
  await assert.rejects(() => deniedClient.createChatCompletion({ model: 'llama3.2:latest' }), (error) => {
    assert.ok(error instanceof XenoApiError);
    assert.equal(error.status, 403);
    assert.equal(error.endpoint, '/v1/chat/completions');
    return true;
  });
});

test('client telemetry methods use expected routes and bodies', async () => {
  const fetchImpl = createFetch((url, options) => jsonResponse({}, options.method === 'DELETE' ? 204 : 200));
  const client = new XenoClient({ baseUrl: 'https://xeno.local', fetchImpl });

  await client.getClientTelemetryStatus();
  await client.getLatestClientTelemetry({ clientId: 'xcl_1', empty: '' });
  await client.collectClientTelemetry({ clientId: 'xcl_1', collectHostTelemetry: true });
  await client.searchClientTelemetry({ clientId: 'xcl_1', metric: 'telemetry.cpu.utilizationPercent' });
  await client.getClientTelemetryRollups({ metric: 'telemetry.cpu.utilizationPercent' });
  await client.getClientTelemetryDetail('ctel_1');
  await client.deleteClientTelemetry('ctel_1');
  await client.getLatestClientProviderTelemetry({ clientId: 'xcl_1' });
  await client.searchClientProviderTelemetry({ providerName: 'ollama' });
  await client.getClientProviderTelemetryRollups({ metric: 'provider.queuedRequests' });
  await client.getClientProviderTelemetryDetail('cptel_1');
  await client.deleteClientProviderTelemetry('cptel_1');
  await client.getClientTelemetryMetricCatalog();

  assert.deepEqual(fetchImpl.calls.map((call) => [call.options.method, call.url]), [
    ['GET', 'https://xeno.local/v1.0/api/client-telemetry/status'],
    ['GET', 'https://xeno.local/v1.0/api/client-telemetry/latest?clientId=xcl_1'],
    ['POST', 'https://xeno.local/v1.0/api/client-telemetry/collect'],
    ['POST', 'https://xeno.local/v1.0/api/client-telemetry/history/search'],
    ['POST', 'https://xeno.local/v1.0/api/client-telemetry/history/rollups'],
    ['GET', 'https://xeno.local/v1.0/api/client-telemetry/history/ctel_1'],
    ['DELETE', 'https://xeno.local/v1.0/api/client-telemetry/history/ctel_1'],
    ['GET', 'https://xeno.local/v1.0/api/client-telemetry/providers/latest?clientId=xcl_1'],
    ['POST', 'https://xeno.local/v1.0/api/client-telemetry/providers/history/search'],
    ['POST', 'https://xeno.local/v1.0/api/client-telemetry/providers/history/rollups'],
    ['GET', 'https://xeno.local/v1.0/api/client-telemetry/providers/history/cptel_1'],
    ['DELETE', 'https://xeno.local/v1.0/api/client-telemetry/providers/history/cptel_1'],
    ['GET', 'https://xeno.local/v1.0/api/client-telemetry/metrics']
  ]);
  assert.match(fetchImpl.calls[2].options.body, /"clientId":"xcl_1"/);
  assert.match(fetchImpl.calls[3].options.body, /"metric":"telemetry.cpu.utilizationPercent"/);
  assert.match(fetchImpl.calls[8].options.body, /"providerName":"ollama"/);
});

test('text and error responses are handled', async () => {
  const metricsFetch = createFetch(() => jsonResponse('xeno_connected_clients 1'));
  const metricsClient = new XenoClient({ baseUrl: 'https://xeno.local', fetchImpl: metricsFetch });
  assert.equal(await metricsClient.getMetrics(), 'xeno_connected_clients 1');

  const errorFetch = createFetch(() => jsonResponse({ message: 'denied' }, 403));
  const errorClient = new XenoClient({ baseUrl: 'https://xeno.local', fetchImpl: errorFetch });
  await assert.rejects(() => errorClient.listClients(), (error) => {
    assert.ok(error instanceof XenoApiError);
    assert.equal(error.status, 403);
    assert.equal(error.endpoint, '/v1.0/api/clients');
    assert.equal(error.message, 'denied');
    return true;
  });
});
