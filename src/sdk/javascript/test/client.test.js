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

test('ping sends heartbeat query parameters', async () => {
  const fetchImpl = createFetch(() => jsonResponse({ status: 'healthy', sequence: 3, serverUtc: '2026-07-14T12:00:01Z' }));
  const client = new XenoClient({ baseUrl: 'https://xeno.local', bearerToken: null, fetchImpl });

  const response = await client.ping({ sequence: 3, clientSentUtc: '2026-07-14T12:00:00Z' });

  assert.equal(response.status, 'healthy');
  assert.equal(response.sequence, 3);
  assert.equal(fetchImpl.calls[0].url, 'https://xeno.local/v1.0/api/ping?sequence=3&clientSentUtc=2026-07-14T12%3A00%3A00Z');
  assert.equal(fetchImpl.calls[0].options.method, 'GET');
  assert.equal(fetchImpl.calls[0].options.headers.Authorization, undefined);
});

test('onboard uses public signup route and surfaces conflict', async () => {
  let callCount = 0;
  const fetchImpl = createFetch(() => {
    callCount += 1;
    if (callCount === 1) {
      return jsonResponse({
        status: 'success',
        tenant: { id: 'ten_1', name: 'Tenant', active: true },
        user: { id: 'usr_1', tenantId: 'ten_1', username: 'owner@xeno', email: 'owner@xeno', isAdmin: false, isTenantAdmin: true, active: true },
        credential: { id: 'cred_1', tenantId: 'ten_1', userId: 'usr_1', accessKey: 'xeno_access', secretKey: 'xeno_secret', active: true },
        token: { token: 'tok_1', tokenType: 'Bearer', tenantId: 'ten_1', userId: 'usr_1', sessionId: 'sess_1' }
      });
    }

    return jsonResponse({ error: 'EmailAlreadyExists', message: 'Email already exists in the tenant.' }, 409);
  });
  const client = new XenoClient({ baseUrl: 'https://xeno.local', bearerToken: null, fetchImpl });

  const response = await client.onboard({ tenantName: 'Tenant', email: 'owner@xeno', username: 'owner@xeno', password: 'password' });
  assert.equal(response.tenant.id, 'ten_1');
  assert.equal(response.credential.secretKey, 'xeno_secret');
  assert.equal(fetchImpl.calls[0].url, 'https://xeno.local/v1.0/api/onboarding/signup');
  assert.equal(fetchImpl.calls[0].options.method, 'POST');
  assert.equal(fetchImpl.calls[0].options.headers.Authorization, undefined);
  assert.match(fetchImpl.calls[0].options.body, /"tenantName":"Tenant"/);

  await assert.rejects(() => client.onboard({ tenantId: 'ten_1', email: 'owner@xeno', password: 'password' }), (error) => {
    assert.ok(error instanceof XenoApiError);
    assert.equal(error.status, 409);
    assert.equal(error.response.error, 'EmailAlreadyExists');
    return true;
  });
});

test('management CRUD methods use expected routes and bodies', async () => {
  const fetchImpl = createFetch((url, options) => jsonResponse({}, options.method === 'DELETE' ? 204 : 200));
  const client = new XenoClient({ baseUrl: 'https://xeno.local', fetchImpl });

  await client.createTenant({ name: 'Tenant' });
  await client.updateTenant('ten_1', { name: 'Tenant 2' });
  await client.deleteTenant('ten_1');
  await client.nukeTenant({ tenantId: 'ten_1', confirmTenantId: 'ten_1', dryRun: true, reason: 'Testing tenant nuke' });
  await client.createUser({ username: 'user@xeno', password: 'password' });
  await client.updateUser('usr_1', { active: false });
  await client.deleteUser('usr_1');
  await client.nukeUser({ tenantId: 'ten_1', userId: 'usr_1', confirmUserId: 'usr_1', dryRun: true, reason: 'Testing user nuke' });
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
  await client.pullClientModel({ clientId: 'xcl_1', model: 'llama3.2:latest', source: 'local-ollama' });
  await client.deleteClientModel({ clientId: 'xcl_1', name: 'llama3.2:latest', source: 'local-ollama' });

  assert.deepEqual(fetchImpl.calls.map((call) => [call.options.method, call.url]), [
    ['POST', 'https://xeno.local/v1.0/api/tenants'],
    ['PUT', 'https://xeno.local/v1.0/api/tenants/ten_1'],
    ['DELETE', 'https://xeno.local/v1.0/api/tenants/ten_1'],
    ['POST', 'https://xeno.local/v1.0/api/admin/nuke/tenant'],
    ['POST', 'https://xeno.local/v1.0/api/users'],
    ['PUT', 'https://xeno.local/v1.0/api/users/usr_1'],
    ['DELETE', 'https://xeno.local/v1.0/api/users/usr_1'],
    ['POST', 'https://xeno.local/v1.0/api/admin/nuke/user'],
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
    ['DELETE', 'https://xeno.local/v1.0/api/routing-policies/rpol_1'],
    ['POST', 'https://xeno.local/v1.0/api/client-models/pull'],
    ['DELETE', 'https://xeno.local/v1.0/api/client-models']
  ]);
  assert.match(fetchImpl.calls[0].options.body, /"name":"Tenant"/);
  assert.match(fetchImpl.calls[3].options.body, /"confirmTenantId":"ten_1"/);
  assert.match(fetchImpl.calls[7].options.body, /"confirmUserId":"usr_1"/);
  assert.match(fetchImpl.calls[12].options.body, /"permissionIds":\["perm_1"\]/);
  assert.match(fetchImpl.calls[19].options.body, /"principalId":"usr_1"/);
  assert.match(fetchImpl.calls[21].options.body, /"modelName":"\*"/);
  assert.match(fetchImpl.calls[24].options.body, /"model":"llama3.2:latest"/);
  assert.match(fetchImpl.calls[25].options.body, /"name":"llama3.2:latest"/);
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

test('ledger methods use expected routes and bodies', async () => {
  const fetchImpl = createFetch((url) => {
    if (url.endsWith('/v1.0/api/ledger/status')) return jsonResponse({ queue: { enabled: true, queueDepth: 1 }, netLedgerHealthy: true });
    if (url.endsWith('/v1.0/api/ledger/balance/me')) return jsonResponse({ tenantId: 'ten_default', userId: 'usr_1', balance: 5 });
    if (url.includes('/v1.0/api/ledger/balances?')) return jsonResponse({ items: [], total: 0, page: 1, pageSize: 100 });
    if (url.endsWith('/v1.0/api/ledger/balances/users/usr_1')) return jsonResponse({ tenantId: 'ten_default', userId: 'usr_1', balance: 5 });
    if (url.endsWith('/v1.0/api/ledger/entries/search')) return jsonResponse({ items: [{ direction: 'Credit', amount: 7 }], total: 1 });
    return jsonResponse({});
  });
  const client = new XenoClient({ baseUrl: 'https://xeno.local', fetchImpl });

  const status = await client.getLedgerStatus();
  assert.equal(status.queue.queueDepth, 1);
  const balance = await client.getMyLedgerBalance();
  assert.equal(balance.balance, 5);
  await client.searchLedgerBalances({ userId: 'usr_1', page: 1, empty: '' });
  await client.getLedgerUserBalance('usr_1');
  await client.getMyLedgerEntries();
  const entries = await client.searchLedgerEntries({ userId: 'usr_1', direction: 'Credit', pageSize: 25 });
  assert.equal(entries.items[0].amount, 7);
  await client.drainLedgerQueue();
  await client.reconcileLedger();

  assert.deepEqual(fetchImpl.calls.map((call) => [call.options.method, call.url]), [
    ['GET', 'https://xeno.local/v1.0/api/ledger/status'],
    ['GET', 'https://xeno.local/v1.0/api/ledger/balance/me'],
    ['GET', 'https://xeno.local/v1.0/api/ledger/balances?userId=usr_1&page=1'],
    ['GET', 'https://xeno.local/v1.0/api/ledger/balances/users/usr_1'],
    ['GET', 'https://xeno.local/v1.0/api/ledger/entries/me'],
    ['POST', 'https://xeno.local/v1.0/api/ledger/entries/search'],
    ['POST', 'https://xeno.local/v1.0/api/ledger/drain'],
    ['POST', 'https://xeno.local/v1.0/api/ledger/reconcile']
  ]);
  assert.match(fetchImpl.calls[5].options.body, /"direction":"Credit"/);
  assert.equal(fetchImpl.calls[6].options.body, '{}');
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

test('updateSettings passes through email settings', async () => {
  const fetchImpl = createFetch(() => jsonResponse({ Email: { Provider: 'File' } }));
  const client = new XenoClient({ baseUrl: 'https://xeno.local', fetchImpl });

  const response = await client.updateSettings({
    Email: {
      Enabled: true,
      Provider: 'File',
      File: { Directory: 'data/email-outbox' }
    }
  });

  assert.equal(fetchImpl.calls[0].url, 'https://xeno.local/v1.0/api/settings');
  assert.equal(fetchImpl.calls[0].options.method, 'POST');
  assert.match(fetchImpl.calls[0].options.body, /"Email":/);
  assert.match(fetchImpl.calls[0].options.body, /"Provider":"File"/);
  assert.equal(response.Email.Provider, 'File');
  assert.equal(response.settings, undefined);
});

test('sendTestEmail uses administrative email route', async () => {
  const fetchImpl = createFetch(() => jsonResponse({
    accepted: true,
    skipped: false,
    provider: 'Mailgun',
    providerMessageId: 'msg_1',
    statusMessage: 'Email accepted by Mailgun.',
    templateId: 'administrative-test',
    toEmail: 'joel.christner@gmail.com',
    subject: 'Xeno test email',
    sentUtc: '2026-07-10T00:00:00Z'
  }));
  const client = new XenoClient({ baseUrl: 'https://xeno.local', fetchImpl });

  const response = await client.sendTestEmail({
    toEmail: 'joel.christner@gmail.com',
    body: 'hello world'
  });

  assert.equal(response.accepted, true);
  assert.equal(response.templateId, 'administrative-test');
  assert.equal(fetchImpl.calls[0].url, 'https://xeno.local/v1.0/api/email/test');
  assert.equal(fetchImpl.calls[0].options.method, 'POST');
  assert.match(fetchImpl.calls[0].options.body, /"toEmail":"joel.christner@gmail.com"/);
  assert.match(fetchImpl.calls[0].options.body, /"body":"hello world"/);
});

test('usage analytics send every parameter and return the response', async () => {
  const fetchImpl = createFetch(() => jsonResponse({ Dataset: 'NetworkRequests', Totals: { Requests: 3 }, Buckets: [], Truncated: false }));
  const client = new XenoClient({ baseUrl: 'https://xeno.local', fetchImpl });
  const result = await client.getUsageAnalytics({ fromUtc: '2026-11-01T00:00:00Z', toUtc: '2026-11-02T00:00:00Z', bucketMinutes: 15, model: 'llama3', provider: 'p', clientId: 'c', userId: 'u', credentialId: 'k', tenantId: 't', groupBy: 'User', groupMetric: 'Tokens', topN: 5, utcOffsetMinutes: -480, sections: 'Totals,Groups' });
  assert.equal(result.Totals.Requests, 3);
  const url = new URL(fetchImpl.calls[0].url);
  assert.equal(url.pathname, '/v1.0/api/request-history/analytics/usage');
  assert.deepEqual(Object.fromEntries(url.searchParams), { fromUtc: '2026-11-01T00:00:00Z', toUtc: '2026-11-02T00:00:00Z', bucketMinutes: '15', model: 'llama3', provider: 'p', clientId: 'c', userId: 'u', credentialId: 'k', tenantId: 't', groupBy: 'User', groupMetric: 'Tokens', topN: '5', utcOffsetMinutes: '-480', sections: 'Totals,Groups' });
});

test('chat usage and provider health use their routes, and an empty query adds nothing', async () => {
  const fetchImpl = createFetch(() => jsonResponse({}));
  const client = new XenoClient({ baseUrl: 'https://xeno.local', fetchImpl });
  await client.getChatUsageAnalytics({ scope: 'tenant' });
  await client.getProviderHealth({ bucketMinutes: 1, clientId: 'c', providerName: 'ollama' });
  await client.getUsageAnalytics();
  assert.deepEqual(fetchImpl.calls.map((call) => call.url), [
    'https://xeno.local/v1.0/api/chat/analytics/usage?scope=tenant',
    'https://xeno.local/v1.0/api/client-telemetry/providers/health?bucketMinutes=1&clientId=c&providerName=ollama',
    'https://xeno.local/v1.0/api/request-history/analytics/usage'
  ]);
});

test('usage analytics surface 400 and 403 as API errors', async () => {
  for (const status of [400, 403]) {
    const client = new XenoClient({ baseUrl: 'https://xeno.local', fetchImpl: createFetch(() => jsonResponse({ Message: 'no' }, status)) });
    await assert.rejects(() => client.getUsageAnalytics({ groupBy: 'Tenant' }), (error) => error instanceof XenoApiError && error.status === status);
  }
});
