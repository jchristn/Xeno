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
  await client.createPermission({ resourceType: 'Clients', operationType: 'Read' });
  await client.updatePermission('perm_1', { permit: false });
  await client.deletePermission('perm_1');
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
    ['POST', 'https://xeno.local/v1.0/api/permissions'],
    ['PUT', 'https://xeno.local/v1.0/api/permissions/perm_1'],
    ['DELETE', 'https://xeno.local/v1.0/api/permissions/perm_1'],
    ['POST', 'https://xeno.local/v1.0/api/routing-policies'],
    ['PUT', 'https://xeno.local/v1.0/api/routing-policies/rpol_1'],
    ['DELETE', 'https://xeno.local/v1.0/api/routing-policies/rpol_1']
  ]);
  assert.match(fetchImpl.calls[0].options.body, /"name":"Tenant"/);
  assert.match(fetchImpl.calls[10].options.body, /"permissionIds":\["perm_1"\]/);
  assert.match(fetchImpl.calls[15].options.body, /"modelName":"\*"/);
});

test('OpenAI and request history methods use expected routes', async () => {
  const fetchImpl = createFetch();
  const client = new XenoClient({ baseUrl: 'https://xeno.local', fetchImpl });

  await client.listOpenAiModels();
  await client.createChatCompletion({ model: 'llama3.2:latest' });
  await client.createEmbedding({ model: 'text-embedding-3-small', input: 'hello' });
  await client.listRequestHistory({ model: 'llama3.2:latest', empty: '' });
  await client.getRequestHistorySummary({ bucketMinutes: 60 });

  assert.deepEqual(fetchImpl.calls.map((call) => [call.options.method, call.url]), [
    ['GET', 'https://xeno.local/v1/models'],
    ['POST', 'https://xeno.local/v1/chat/completions'],
    ['POST', 'https://xeno.local/v1/embeddings'],
    ['GET', 'https://xeno.local/v1.0/api/request-history?model=llama3.2%3Alatest'],
    ['GET', 'https://xeno.local/v1.0/api/request-history/summary?bucketMinutes=60']
  ]);
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
