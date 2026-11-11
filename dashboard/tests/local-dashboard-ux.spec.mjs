import { expect, test } from '@playwright/test';
import { emptyUsage, healthStrip, usageAnalytics } from './usageFixture.mjs';

let logRequests = 0;

const stateResponse = {
  dashboardUrl: 'http://127.0.0.1:9343',
  dashboardStatus: 'Listening',
  connection: {
    isConnected: true,
    isRegistered: true,
    clientId: 'xcl_local_test',
    tenantId: 'ten_default',
    webSocketUrl: 'ws://localhost:9000/ws/v1/clients',
    lastDisconnectReason: ''
  },
  settings: {
    localDashboard: {
      enabled: true,
      host: '127.0.0.1',
      port: 9343,
      requireLocalToken: true,
      openBrowserOnStart: false,
      database: { filename: '~/.xeno/local-dashboard.db' },
      historyRetention: {
        maxEntries: 1000,
        maxBodyBytes: 65536,
        persistBodies: true
      }
    }
  }
};

const providersResponse = {
  providers: [
    {
      name: 'local-ollama',
      apiType: 'Ollama',
      endpoint: 'http://127.0.0.1:11434',
      timeoutMs: 300000,
      credential: null,
      models: [{ name: 'llama3.2:latest', supportsCompletions: true, supportsEmbeddings: false }]
    }
  ]
};

const modelsResponse = {
  models: [
    {
      provider: 'local-ollama',
      apiType: 'Ollama',
      endpoint: 'http://127.0.0.1:11434',
      name: 'llama3.2:latest',
      supportsCompletions: true,
      supportsEmbeddings: false
    }
  ]
};

const requestEntry = {
  id: 'req_local_1',
  tenantId: 'ten_default',
  requestorTenantId: 'ten_default',
  requestorPrincipalType: 'User',
  requestorPrincipalId: 'usr_local_requestor',
  clientId: 'xcl_local_test',
  receivedUtc: '2026-06-26T08:00:00Z',
  completedUtc: '2026-06-26T08:00:01Z',
  method: 'POST',
  path: '/v1/chat/completions',
  requestorIp: '127.0.0.1',
  providerName: 'local-ollama',
  apiType: 'Ollama',
  modelName: 'llama3.2:latest',
  statusCode: 200,
  outcome: 'Success',
  streamed: true,
  requestHeaders: { 'content-type': 'application/json' },
  responseHeaders: { 'content-type': 'text/event-stream' },
  requestBody: '{"model":"llama3.2:latest"}',
  responseBody: 'data: {"choices":[{"delta":{"content":"ok"}}]}',
  telemetry: { durationMs: 42, timeToFirstByteMs: 12, streamChunkCount: 2, streamEventCount: 2 },
  timeline: [{ stage: 'Received', timestampUtc: '2026-06-26T08:00:00Z', message: 'Request received.' }]
};

const telemetryLatestResponse = {
  status: {
    enabled: true,
    running: true,
    collectionIntervalMs: 5000,
    retentionDays: 7,
    samples: 2,
    newestSampleUtc: '2026-06-26T09:00:00Z',
    lastCollectionUtc: '2026-06-26T09:00:00Z'
  },
  detail: {
    sample: {
      id: 'tel_local_1',
      machineName: 'local-test',
      collectedUtc: '2026-06-26T09:00:00Z',
      cpuUtilizationPercent: 34,
      memoryUtilizationPercent: 48,
      diskUtilizationPercent: 62,
      gpuAverageUtilizationPercent: 21
    },
    gpus: []
  },
  snapshot: { gpu: { devices: [] } }
};

test.beforeEach(async ({ page }) => {
  logRequests = 0;
  await page.route('**/api/v1/state', async (route) => route.fulfill({ json: stateResponse }));
  await page.route('**/api/v1/settings', async (route) => route.fulfill({ json: stateResponse }));
  await page.route('**/api/v1/local-inference/routing', async (route) => route.fulfill({ json: routingResponse }));
  await page.route('**/api/v1/statistics', async (route) => route.fulfill({ json: { requests: 12, failures: 1, streamingRequests: 3, averageLatencyMs: 42 } }));
  await page.route('**/api/v1/requests/summary**', async (route) => route.fulfill({ json: { buckets: [{ startUtc: '2026-06-26T08:00:00Z', total: 4, success: 4, failure: 0 }, { startUtc: '2026-06-26T09:00:00Z', total: 8, success: 7, failure: 1 }] } }));
  await page.route('**/api/v1/providers*/test', async (route) => route.fulfill({ json: { name: 'local-ollama', endpoint: 'http://127.0.0.1:11434', success: true, statusCode: 200 } }));
  await page.route('**/api/v1/providers**', async (route) => route.fulfill({ json: route.request().method() === 'GET' ? providersResponse : { removed: 1, removedOne: true } }));
  await page.route('**/api/v1/models**', async (route) => route.fulfill({ json: route.request().method() === 'GET' ? modelsResponse : { removed: 1, removedOne: true } }));
  await page.route('**/api/v1/requests/req_local_1', async (route) => route.fulfill({ json: requestEntry }));
  await page.route('**/api/v1/requests/bulk', async (route) => route.fulfill({ json: { removed: 1, removedOne: true } }));
  await page.route('**/api/v1/requests', async (route) => route.fulfill({ json: { entries: [requestEntry] } }));
  await page.route('**/api/v1/telemetry/latest', async (route) => route.fulfill({ json: telemetryLatestResponse }));
  await page.route('**/api/v1/telemetry/history/rollups**', async (route) => {
    const body = route.request().postDataJSON();
    const base = body.metric === 'MemoryUtilizationPercent' ? 48 : body.metric === 'GpuUtilizationPercent' ? 21 : 34;
    await route.fulfill({
      json: [
        { startUtc: '2026-06-26T08:00:00Z', endUtc: '2026-06-26T08:15:00Z', label: '08:00', count: 1, average: base, minimum: base - 2, maximum: base + 2 },
        { startUtc: '2026-06-26T08:15:00Z', endUtc: '2026-06-26T08:30:00Z', label: '08:15', count: 1, average: base + 4, minimum: base + 1, maximum: base + 5 }
      ]
    });
  });
  await page.route('**/api/v1/logs**', async (route) => {
    logRequests += 1;
    await route.fulfill({
      json: {
        logFile: 'xeno.log',
        lines: [
          '2026-06-26 08:00:00.000000Z [INFO] Local dashboard listening.',
          `2026-06-26 08:00:${String(Math.min(logRequests, 59)).padStart(2, '0')}.000000Z [DEBUG] Latest local dashboard log.`
        ]
      }
    });
  });
  await page.route('**/api/v1/database', async (route) => route.fulfill({ json: { status: 'Ready', filename: '~/.xeno/local-dashboard.db', resolvedFilename: 'C:\\Users\\test\\.xeno\\local-dashboard.db', exists: true, sizeBytes: 4096, requests: 12, oldestRequestUtc: '2026-06-26T08:00:00Z', newestRequestUtc: '2026-06-26T09:00:00Z' } }));
});

test('local dashboard renders and follows interaction expectations', async ({ page }) => {
  const pageErrors = [];
  const consoleErrors = [];
  page.on('pageerror', (error) => pageErrors.push(error.message));
  page.on('console', (message) => {
    if (message.type() === 'error') consoleErrors.push(message.text());
  });
  await page.goto('/index-local.html');
  await page.waitForTimeout(500);
  expect(pageErrors).toEqual([]);
  expect(consoleErrors).toEqual([]);
  await expect(page).toHaveTitle('Xeno Client');
  await expect(page.locator('.sidebar-brand')).toContainText('Xeno Client');
  await expect(page.getByRole('heading', { name: 'Home' })).toBeVisible();
  await expect(page.locator('.activity-chart')).toBeVisible();
  expect(await page.locator('.activity-chart rect').count()).toBeGreaterThan(0);
  await expect(page.getByRole('button', { name: 'Last Hour' })).toBeVisible();
  await expect(page.locator('.topbar-server-url')).toContainText('http://127.0.0.1:9343');
  await expect(page.locator('.metric-card', { hasText: 'Connected' })).toContainText('ws://localhost:9000/ws/v1/clients');

  await page.getByTitle('Switch between light and dark dashboard themes without changing your account or server settings.').click();
  await expect(page.locator('html')).toHaveAttribute('data-theme', 'dark');
  await page.setViewportSize({ width: 390, height: 844 });
  await expect(page.locator('.app-shell')).toBeVisible();
  await page.setViewportSize({ width: 1366, height: 900 });

  await page.getByRole('button', { name: 'Providers', exact: true }).click();
  await expect(page.getByRole('heading', { name: 'Provider Endpoints' })).toBeVisible();
  await page.getByPlaceholder('Filter Name').fill('local');
  await page.keyboard.press('Enter');
  await expect(page.getByText('local-ollama').first()).toBeVisible();
  await page.getByText('local-ollama').first().click();
  await expect(page.getByRole('heading', { name: 'Edit Provider' })).toBeVisible();
  const providerFormFonts = await page.getByRole('dialog').locator('input[type="text"], input:not([type]), select, .toggle-field').evaluateAll((nodes) => nodes.map((node) => parseFloat(getComputedStyle(node).fontSize)));
  expect(providerFormFonts.length).toBeGreaterThan(0);
  expect(providerFormFonts.every((size) => size <= 14)).toBe(true);
  await page.getByTitle('Close this modal without applying unsaved changes.').click();

  await page.getByRole('button', { name: 'Advertised Models', exact: true }).click();
  await expect(page.locator('h1', { hasText: 'Advertised Models' })).toBeVisible();
  await page.getByRole('button', { name: 'Create Model' }).click();
  await expect(page.getByRole('heading', { name: 'Create Model' })).toBeVisible();
  await page.getByTitle('Close this modal without applying unsaved changes.').click();

  await page.getByRole('button', { name: 'Request History', exact: true }).click();
  await expect(page.locator('h1', { hasText: 'Request History' })).toBeVisible();
  await page.getByText('req_local_1').click();
  await expect(page.getByRole('heading', { name: 'Request Details' })).toBeVisible();
  await expect(page.locator('.modal-panel.request-details')).toBeVisible();
  const requestDetailsBox = await page.locator('.modal-panel.request-details').boundingBox();
  expect(requestDetailsBox.width).toBeGreaterThan(1200);
  expect(requestDetailsBox.height).toBeGreaterThan(800);
  await expect(page.getByText('Request Headers')).toBeVisible();
  await expect(page.getByRole('dialog').getByText('usr_local_requestor')).toBeVisible();
  await expect(page.getByText('Response Body')).toBeVisible();
  await page.getByTitle('Close this modal without applying unsaved changes.').click();

  await page.getByRole('button', { name: 'Logs', exact: true }).click();
  await expect(page.getByText('[INFO] Local dashboard listening.')).toBeVisible();
  const logPreviewText = await page.locator('.client-logs-card .json-preview').innerText();
  expect(logPreviewText.indexOf('[DEBUG] Latest local dashboard log.')).toBeLessThan(logPreviewText.indexOf('[INFO] Local dashboard listening.'));
  await page.getByLabel('Follow live').check();
  await expect.poll(() => logRequests, { timeout: 3000 }).toBeGreaterThan(1);

  await page.getByRole('button', { name: 'Host', exact: true }).click();
  await expect(page.locator('[data-chart="telemetry.host"]')).toHaveCount(1);
  await expect(page.locator('[data-chart="telemetry.host"] h2')).toContainText(', last day');
  await expect(page.locator('.chart-refresh button')).toBeVisible();

  await page.getByRole('button', { name: 'Settings', exact: true }).click();
  await expect(page.getByRole('heading', { name: 'Database Status' })).toBeVisible();

  const missingTooltips = await page.locator('button, a, input, select').evaluateAll((nodes) => nodes
    .filter((node) => !node.getAttribute('title') && !node.getAttribute('aria-label'))
    .map((node) => node.outerHTML));
  expect(missingTooltips).toEqual([]);
});

test('local dashboard shows the port notice and saves routing settings without dropping route rules', async ({ page }) => {
  const migratedState = {
    ...stateResponse,
    connection: { ...stateResponse.connection, localInferencePortMigratedFrom: 11434, localInferencePortMigratedTo: 11435 },
    settings: {
      ...stateResponse.settings,
      defaultMaxConcurrentRequests: 4,
      localInferenceServer: {
        enabled: true,
        host: '127.0.0.1',
        port: 11435,
        enableCors: true,
        requestTimeoutMs: 300000,
        maxRequestBodyBytes: 10485760,
        routingMode: 'PreferLocal',
        routes: [{ model: 'gpt-oss:120b', target: 'Network', provider: null, fallbackToNetwork: false }],
        discoverLocalModels: true,
        healthCheckIntervalMs: 15000,
        localQueueTimeoutMs: 30000
      }
    }
  };
  let acknowledged = 0;
  let savedSettings = null;
  await page.route('**/api/v1/state', async (route) => route.fulfill({ json: migratedState }));
  await page.route('**/api/v1/settings', async (route) => {
    if (route.request().method() === 'PUT') savedSettings = route.request().postDataJSON();
    await route.fulfill({ json: migratedState });
  });
  await page.route('**/api/v1/local-inference/port-migration/acknowledge', async (route) => {
    acknowledged += 1;
    await route.fulfill({ json: { acknowledged: true } });
  });

  await page.goto('/index-local.html');
  await expect(page.getByText('The local inference port moved from 11434 to 11435')).toBeVisible();
  await page.getByRole('button', { name: 'Dismiss' }).click();
  await expect.poll(() => acknowledged).toBe(1);

  await page.getByRole('button', { name: 'Settings', exact: true }).click();
  const routing = page.locator('select[name="localInferenceRoutingMode"]');
  await expect(routing).toHaveValue('PreferLocal');
  await expect(page.locator('input[name="localInferencePort"]')).toHaveValue('11435');
  await expect(page.locator('input[name="defaultMaxConcurrentRequests"]')).toHaveValue('4');
  await routing.selectOption('LocalOnly');
  await page.locator('input[name="defaultMaxConcurrentRequests"]').fill('6');
  await routing.locator('xpath=ancestor::form').getByRole('button', { name: 'Save Settings' }).click();
  await expect.poll(() => savedSettings).not.toBeNull();
  expect(savedSettings.localInferenceServer.routingMode).toBe('LocalOnly');
  expect(savedSettings.defaultMaxConcurrentRequests).toBe(6);
  expect(savedSettings.localInferenceServer.routes).toHaveLength(1);
  expect(savedSettings.localInferenceServer.routes[0].model).toBe('gpt-oss:120b');
  expect(Object.keys(savedSettings.localInferenceServer).some((key) => key[0] === key[0].toUpperCase())).toBe(false);
});

const routingResponse = {
  enabled: true,
  url: 'http://127.0.0.1:11435',
  routingMode: 'PreferLocal',
  runtimeAvailable: true,
  updatedUtc: new Date().toISOString(),
  rules: [{ model: 'gpt-oss:120b', target: 'Network', provider: null, fallbackToNetwork: false }],
  backends: [
    { provider: 'local-ollama', apiType: 'Ollama', endpoint: 'http://127.0.0.1:11434', health: 'healthy', latencyMs: 12.4, lastError: '', inFlight: 1, inFlightLocal: 1, inFlightNetwork: 0, queued: 0, limit: 4, limitSource: 'global', shared: true, routable: true, discoveredModels: 3 },
    { provider: 'vllm', apiType: 'vLLM', endpoint: 'http://127.0.0.1:8000', health: 'unhealthy', latencyMs: null, lastError: 'Connection refused', inFlight: 0, inFlightLocal: 0, inFlightNetwork: 0, queued: 0, limit: 4, limitSource: 'global', shared: false, routable: true, discoveredModels: 0 }
  ],
  localRequests: 7,
  networkRequests: 3,
  fallbacks: 1,
  errors: 1,
  defaultMaxConcurrentRequests: 4,
  healthyBackends: 1,
  routableBackends: 2
};

test('local endpoint page shows routing, edits rules, and tests routes', async ({ page }) => {
  let savedRule = null;
  let deletedModel = null;
  const now = Date.now();
  await page.route('**/api/v1/local-inference/routing', async (route) => route.fulfill({ json: routingResponse }));
  await page.route('**/api/v1/local-inference/requests**', async (route) => route.fulfill({ json: { items: [
    { id: 'r1', receivedUtc: new Date(now - 60000).toISOString(), method: 'POST', path: '/v1/chat/completions', model: 'llama3.1:8b', route: 'local:local-ollama', provider: 'local-ollama', reason: 'preferLocal', statusCode: 200, latencyMs: 812.2, queuedMs: 0, promptTokens: 12, completionTokens: 40, fallback: false },
    { id: 'r2', receivedUtc: new Date(now - 120000).toISOString(), method: 'POST', path: '/api/chat', model: 'deepseek-r1:671b', route: 'network', provider: '', reason: 'notLocal', statusCode: 200, latencyMs: 2412, queuedMs: 0, fallback: false }
  ], total: 2, page: 1, pageSize: 200 } }));
  await page.route('**/api/v1/local-inference/routes', async (route) => {
    savedRule = route.request().postDataJSON();
    await route.fulfill({ json: savedRule });
  });
  await page.route('**/api/v1/local-inference/routes/**', async (route) => {
    deletedModel = decodeURIComponent(new URL(route.request().url()).pathname.split('/').pop());
    await route.fulfill({ json: { removed: 1, removedOne: true } });
  });
  await page.route('**/api/v1/local-inference/resolve**', async (route) => route.fulfill({ json: { model: 'llama3.1:8b', normalizedModel: 'llama3.1:8b', target: 'local', route: 'local:local-ollama', provider: 'local-ollama', reason: 'preferLocal', reasonText: 'x', fallbackToNetwork: false, shapeSupported: true } }));

  await page.goto('/index-local.html');
  await page.getByRole('button', { name: 'Local endpoint', exact: true }).click();
  await expect(page.getByRole('heading', { name: 'Providers on this machine' })).toBeVisible();
  await expect(page.getByText('1 / 2')).toBeVisible();
  await expect(page.getByText('Connection refused')).toBeVisible();
  await expect(page.getByRole('heading', { name: 'Route rules' })).toBeVisible();
  await expect(page.getByText('gpt-oss:120b')).toBeVisible();
  await expect(page.getByText('deepseek-r1:671b')).toBeVisible();
  await expect(page.locator('.route-mix-chart svg')).toBeVisible();
  await expect(page.locator('.endpoint-hero')).toContainText('http://127.0.0.1:11435/v1');
  await expect(page.locator('.data-table td.column-inFlight', { hasText: '1 / 4' })).toBeVisible();
  for (const width of [1366, 1024, 700]) {
    await page.setViewportSize({ width, height: 900 });
    const overflowing = await page.locator('.endpoint-hero *, .metric-card *').evaluateAll((nodes) => nodes.filter((node) => {
      const box = node.closest('.endpoint-hero, .metric-card').getBoundingClientRect();
      const rect = node.getBoundingClientRect();
      return rect.width > 0 && (rect.right > box.right + 1 || rect.left < box.left - 1);
    }).map((node) => node.outerHTML.slice(0, 120)));
    expect(overflowing, `text outside its card at ${width}px`).toEqual([]);
  }
  await page.setViewportSize({ width: 1366, height: 900 });

  await page.getByRole('button', { name: 'Add route rule' }).click();
  const dialog = page.getByRole('dialog');
  await dialog.locator('input[name="model"]').fill('llama3.1:8b');
  await dialog.locator('select[name="target"]').selectOption('local-ollama');
  await dialog.locator('input[name="fallbackToNetwork"]').check();
  await dialog.getByRole('button', { name: 'Save' }).click();
  await expect.poll(() => savedRule).not.toBeNull();
  expect(savedRule.model).toBe('llama3.1:8b');
  expect(savedRule.target).toBe('Local');
  expect(savedRule.provider).toBe('local-ollama');
  expect(savedRule.fallbackToNetwork).toBe(true);

  await page.getByRole('button', { name: 'Test a route' }).first().click();
  const resolver = page.getByRole('dialog');
  await resolver.locator('input').first().fill('llama3.1:8b');
  await resolver.getByRole('button', { name: 'Test a route' }).click();
  await expect(resolver.getByText('llama3.1:8b goes to local:local-ollama')).toBeVisible();
  await page.getByTitle('Close this modal without applying unsaved changes.').click();

  await page.getByText('gpt-oss:120b').click();
  await expect(page.getByRole('dialog').getByText('Edit route rule')).toBeVisible();
  await page.getByTitle('Close this modal without applying unsaved changes.').click();

  const missingTooltips = await page.locator('button, a, input, select').evaluateAll((nodes) => nodes
    .filter((node) => !node.getAttribute('title') && !node.getAttribute('aria-label'))
    .map((node) => node.outerHTML));
  expect(missingTooltips).toEqual([]);
});

test('API explorer resolves routes and shows the route header', async ({ page }) => {
  await page.route('**/api/v1/local-inference/resolve**', async (route) => route.fulfill({ json: { model: 'qwen3', normalizedModel: 'qwen3:latest', target: 'network', route: 'network', provider: '', reason: 'notLocal', reasonText: 'x', fallbackToNetwork: false, shapeSupported: true } }));
  await page.route('http://127.0.0.1:11435/**', async (route) => route.fulfill({ json: { object: 'list', data: [] }, headers: { 'x-xeno-route': 'local+network', 'access-control-expose-headers': 'x-xeno-route' } }));
  await page.route('**/api/v1/state', async (route) => route.fulfill({ json: { ...stateResponse, connection: { ...stateResponse.connection, localInferenceServerEnabled: true, localInferenceServerListening: true, localInferenceServerUrl: 'http://127.0.0.1:11435' } } }));
  await page.goto('/index-local.html');
  await page.getByRole('button', { name: 'API Explorer', exact: true }).click();
  await expect(page.getByRole('heading', { name: 'Where does a model go?' })).toBeVisible();
  await page.locator('.route-resolver input').fill('qwen3');
  await page.locator('.route-resolver').getByRole('button', { name: 'Test a route' }).click();
  await expect(page.getByText('qwen3 goes to network')).toBeVisible();
  await page.getByRole('button', { name: 'Send Request' }).click();
  await expect(page.getByText('X-Xeno-Route: local+network')).toBeVisible();
});

test('chat fills the window and says in the conversation why a model did not answer', async ({ page }) => {
  const now = new Date().toISOString();
  const conversation = { id: 'cnv_f', title: 'Failing', model: 'llama3.1:8b', toolsEnabled: false, pinned: false, surface: 'Web', messageCount: 0, createdUtc: now, updatedUtc: now, messages: [] };
  await page.route('**/api/v1/chat/models', async (route) => route.fulfill({ json: { entries: [{ name: 'llama3.1:8b', servedLocally: true, onNetwork: false, supportsCompletions: true }], networkMessage: '' } }));
  await page.route('**/api/v1/chat/models/prepare', async (route) => route.fulfill({ json: { model: 'llama3.1:8b', route: 'local:ollama', loaded: true, supportsTools: false, message: 'Loaded.' } }));
  await page.route('**/api/v1/chat/conversations?**', async (route) => route.fulfill({ json: conversation.messages.length ? [{ ...conversation, messages: undefined, messageCount: conversation.messages.length }] : [] }));
  await page.route('**/api/v1/chat/conversations', async (route) => route.fulfill({ status: 201, json: { ...conversation, messages: undefined } }));
  await page.route('**/api/v1/chat/conversations/cnv_f', async (route) => route.fulfill({ json: conversation }));
  await page.route('**/api/v1/chat/conversations/cnv_f/messages', async (route) => {
    const sent = route.request().postDataJSON();
    const failure = { Kind: 'Timeout', Reason: "Provider 'ollama' did not respond within 300 seconds.", StatusCode: 504 };
    conversation.messages = [
      { id: 'msg_u1', conversationId: 'cnv_f', sequence: 1, role: 'User', content: sent.content, createdUtc: now },
      { id: 'msg_a1', conversationId: 'cnv_f', sequence: 2, role: 'Assistant', content: '', model: 'llama3.1:8b', route: 'local:ollama', incomplete: true, failure: { kind: 'Timeout', reason: failure.Reason, statusCode: 504 }, turnId: 'trn_f', createdUtc: now },
      { id: 'msg_u2', conversationId: 'cnv_f', sequence: 3, role: 'User', content: 'earlier', createdUtc: now },
      { id: 'msg_a2', conversationId: 'cnv_f', sequence: 4, role: 'Assistant', content: 'Half an', model: 'llama3.1:8b', route: 'local:ollama', incomplete: true, failure: { kind: 'Interrupted', reason: "Provider 'ollama' stopped in the middle of its response." }, turnId: 'trn_g', createdUtc: now }
    ];
    const events = [
      { Kind: 'Turn', TurnId: 'trn_f', ConversationId: 'cnv_f', MessageId: 'msg_u1' },
      { Kind: 'Error', TurnId: 'trn_f', Text: 'The model timed out: ' + failure.Reason, Failure: failure },
      { Kind: 'Done', TurnId: 'trn_f', MessageId: 'msg_a1', Turn: { Id: 'trn_f', Status: 'Error' } }
    ];
    await route.fulfill({ status: 200, contentType: 'text/event-stream', body: events.map((item) => `event: ${item.Kind.toLowerCase()}\ndata: ${JSON.stringify(item)}\n\n`).join('') });
  });

  for (const size of [{ width: 1440, height: 900 }, { width: 1280, height: 720 }]) {
    await page.setViewportSize(size);
    await page.goto('/index-local.html');
    await page.getByRole('button', { name: 'Chat', exact: true }).click();
    await expect(page.locator('.chat-layout')).toBeVisible();
    // The chat reaches the bottom of the window (inside the page's bottom padding) without scrolling the page.
    const layout = await page.locator('.chat-layout').boundingBox();
    expect(size.height - (layout.y + layout.height)).toBeGreaterThanOrEqual(0);
    expect(size.height - (layout.y + layout.height)).toBeLessThanOrEqual(24);
    expect(await page.evaluate(() => document.documentElement.scrollHeight - window.innerHeight)).toBeLessThanOrEqual(1);
  }

  await page.getByRole('textbox', { name: 'Message' }).fill('Are you there?');
  await page.getByRole('textbox', { name: 'Message' }).press('Enter');
  const failures = page.locator('.chat-message.assistant .chat-failure');
  await expect(failures).toHaveCount(2);
  await expect(failures.nth(0)).toHaveAttribute('data-failure-kind', 'Timeout');
  await expect(failures.nth(0)).toHaveText("The model timed out: Provider 'ollama' did not respond within 300 seconds.");
  await expect(failures.nth(1)).toHaveText("The answer was cut off: Provider 'ollama' stopped in the middle of its response.");
  await expect(page.locator('.chat-message.assistant').nth(1)).toContainText('Half an');
  // A failure with no answer offers nothing to copy or rate.
  await expect(page.locator('.chat-message.assistant').nth(0).locator('.chat-actions')).toHaveCount(0);
});

test('chat streams answers, records feedback, and shows history and ratings', async ({ page }) => {
  const now = new Date().toISOString();
  const conversation = { id: 'cnv_1', title: 'New chat', model: 'llama3.1:8b', toolsEnabled: false, pinned: false, surface: 'Web', messageCount: 0, createdUtc: now, updatedUtc: now, messages: [] };
  let sent = null;
  let feedback = null;
  await page.route('**/api/v1/chat/models', async (route) => route.fulfill({ json: { entries: [
    { name: 'llama3.1:8b', servedLocally: true, onNetwork: false, supportsCompletions: true },
    { name: 'deepseek-r1:671b', servedLocally: false, onNetwork: true, supportsCompletions: true },
    { name: 'nomic-embed-text', servedLocally: true, onNetwork: false, supportsCompletions: false, supportsEmbeddings: true },
    { name: 'all-minilm:latest', servedLocally: true, onNetwork: false, supportsCompletions: true, supportsEmbeddings: true }
  ], networkMessage: '' } }));
  const prepared = [];
  let created = null;
  await page.route('**/api/v1/chat/models/prepare', async (route) => {
    const model = route.request().postDataJSON().model;
    prepared.push(model);
    await route.fulfill({ json: { model, route: model === 'llama3.1:8b' ? 'local:ollama' : 'network', loaded: model === 'llama3.1:8b', supportsTools: model === 'llama3.1:8b', message: 'Loaded.' } });
  });
  await page.route('**/api/v1/chat/conversations?**', async (route) => route.fulfill({ json: conversation.messages.length ? [{ ...conversation, messages: undefined, messageCount: conversation.messages.length }] : [] }));
  await page.route('**/api/v1/chat/conversations', async (route) => {
    const body = route.request().postDataJSON();
    created = body;
    conversation.model = body.model || conversation.model;
    await route.fulfill({ status: 201, json: { ...conversation, messages: undefined } });
  });
  await page.route('**/api/v1/chat/conversations/cnv_1', async (route) => route.fulfill({ json: conversation }));
  await page.route('**/api/v1/chat/conversations/cnv_1/messages', async (route) => {
    sent = route.request().postDataJSON();
    conversation.title = 'Say hi';
    conversation.messages = [
      { id: 'msg_u', conversationId: 'cnv_1', sequence: 1, role: 'User', content: sent.content, createdUtc: now },
      { id: 'msg_a', conversationId: 'cnv_1', sequence: 2, role: 'Assistant', content: 'Hello **there**', thinking: 'The user wants a greeting.', model: 'llama3.1:8b', route: 'local:ollama', completionTokens: 3, timeToFirstTokenMs: 120, totalMs: 900, turnId: 'trn_1', stats: { model: 'llama3.1:8b', route: 'local:ollama', provider: 'ollama', streamed: true, timeToFirstTokenMs: 120, streamingMs: 640, modelMs: 900, toolMs: 4, toolCalls: 1, modelCalls: 2, turnMs: 950, promptTokens: 9, completionTokens: 3, reasoningTokens: 2, turnPromptTokens: 20, turnCompletionTokens: 5, tokensPerSecond: 4.7 }, toolCalls: [{ id: 'tcl_1', callId: 'a', toolName: 'xeno_status', arguments: '{}', result: '{"running":true}', approval: 'Auto', success: true, durationMs: 4 }], createdUtc: now }
    ];
    const events = [
      { Kind: 'Turn', TurnId: 'trn_1', ConversationId: 'cnv_1', MessageId: 'msg_u' },
      { Kind: 'Thinking', TurnId: 'trn_1', Text: 'The user wants a greeting.' },
      { Kind: 'Chunk', TurnId: 'trn_1', Text: 'Hello ' },
      { Kind: 'Chunk', TurnId: 'trn_1', Text: '**there**' },
      { Kind: 'Done', TurnId: 'trn_1', MessageId: 'msg_a', Turn: { Id: 'trn_1', Status: 'Success' } }
    ];
    await new Promise((resolve) => setTimeout(resolve, 700));
    await route.fulfill({ status: 200, contentType: 'text/event-stream', body: events.map((item) => `event: ${item.Kind.toLowerCase()}\ndata: ${JSON.stringify(item)}\n\n`).join('') });
  });
  await page.route('**/api/v1/chat/messages/msg_a/feedback', async (route) => {
    feedback = route.request().postDataJSON();
    conversation.messages[1].feedback = { id: 'fbk_1', rating: feedback.rating, comment: feedback.comment, messageId: 'msg_a' };
    await route.fulfill({ json: conversation.messages[1].feedback });
  });
  const turn = { id: 'trn_1', conversationId: 'cnv_1', conversationTitle: 'Say hi', model: 'llama3.1:8b', route: 'local:ollama', provider: 'ollama', status: 'Success', totalMs: 900, timeToFirstTokenMs: 120, completionTokens: 3, promptTokens: 9, modelCalls: 2, toolCallCount: 1, toolMs: 4, surface: 'Web', feedbackRating: -1, createdUtc: now, toolCalls: [] };
  const analyticsBuckets = [];
  await page.route('**/api/v1/chat/analytics**', async (route) => analyticsBuckets.push(new URL(route.request().url()).searchParams.get('bucketSeconds')) && route.fulfill({ json: { turns: 1, local: 1, network: 0, fallbacks: 0, errors: 0, errorRate: 0, p50Ms: 900, p95Ms: 900, p50TimeToFirstTokenMs: 120, promptTokens: 9, completionTokens: 3, toolCalls: 1, thumbsUp: 0, thumbsDown: 1, negativeRate: 1, bucketMinutes: 60, buckets: [{ startUtc: now, local: 1, network: 0, errors: 0 }], models: [{ model: 'llama3.1:8b', turns: 1, errors: 0, p50Ms: 900, thumbsUp: 0, thumbsDown: 1, completionTokens: 3 }] } }));
  await page.route('**/api/v1/chat/history?**', async (route) => route.fulfill({ json: { items: [turn], total: 1, page: 1, pageSize: 500 } }));
  await page.route('**/api/v1/chat/history/trn_1', async (route) => route.fulfill({ json: turn }));
  await page.route('**/api/v1/chat/feedback?**', async (route) => route.fulfill({ json: { items: [{ id: 'fbk_1', conversationId: 'cnv_1', messageId: 'msg_a', rating: -1, comment: 'Too short', model: 'llama3.1:8b', route: 'local:ollama', conversationTitle: 'Say hi', excerpt: 'Hello there', updatedUtc: now, createdUtc: now }], total: 1, page: 1, pageSize: 500 } }));

  await page.goto('/index-local.html');
  await page.getByRole('button', { name: 'Chat', exact: true }).click();
  const picker = page.getByRole('combobox', { name: 'Model' });
  await expect(picker.locator('optgroup[label="This machine"] option')).toHaveText(['llama3.1:8b']);
  await expect(picker.locator('optgroup[label="Xeno network"] option')).toHaveText(['deepseek-r1:671b']);
  await expect(picker.locator('option', { hasText: 'all-minilm' })).toHaveCount(0);
  await picker.selectOption('deepseek-r1:671b');
  await expect(picker).toHaveValue('deepseek-r1:671b');
  await expect.poll(() => prepared.includes('deepseek-r1:671b')).toBe(true);
  await picker.selectOption('llama3.1:8b');
  await expect(picker).toHaveValue('llama3.1:8b');
  await expect(page.locator('.chat-model-status')).toHaveText('Ready');
  await expect(page.getByText('AI can make mistakes. Please check all work.')).toBeVisible();
  const box = await page.getByRole('textbox', { name: 'Message' }).boundingBox();
  const sendBox = await page.getByRole('button', { name: 'Send' }).boundingBox();
  expect(Math.abs(box.height - sendBox.height)).toBeLessThanOrEqual(1);
  const mainBox = await page.locator('.chat-main').boundingBox();
  expect(await page.locator('.chat-messages').evaluate((element) => parseFloat(getComputedStyle(element).paddingLeft))).toBeLessThanOrEqual(16);
  expect(box.x - mainBox.x).toBeLessThanOrEqual(16);
  expect(mainBox.x + mainBox.width - (sendBox.x + sendBox.width)).toBeLessThanOrEqual(16);
  await expect(page.getByRole('button', { name: 'Streaming on' })).toHaveAttribute('aria-pressed', 'true');
  // A new chat works in the home folder until another folder is chosen; the choice goes with the new conversation.
  const folderChip = page.locator('.chat-directory');
  await expect(folderChip).toHaveText('Home folder');
  await folderChip.click();
  await page.getByRole('dialog').locator('input[name="workingDirectory"]').fill('/tmp/project');
  await page.getByRole('dialog').getByRole('button', { name: 'Save' }).click();
  await expect(folderChip).toHaveText('/tmp/project');
  // A new chat shows the tools it will start with, and they can be changed before the first message.
  const toolsButton = page.locator('.chat-tools-toggle').filter({ hasText: /^Tools/ });
  await expect(toolsButton).toHaveText('Tools on');
  await expect(toolsButton).toBeEnabled();
  await toolsButton.click();
  await expect(toolsButton).toHaveText('Tools off');
  await toolsButton.click();
  await expect(toolsButton).toHaveText('Tools on');
  await page.getByRole('textbox', { name: 'Message' }).fill('Say hi');
  await page.getByRole('textbox', { name: 'Message' }).press('Enter');
  await expect(page.getByRole('button', { name: 'Stop' })).toBeVisible();
  // While nothing has arrived, a short quip shows under the pending bubble; it goes away with the answer.
  await expect(page.locator('.chat-message.assistant.live .chat-wait-label')).toHaveText(/\.\.\.$/);
  await expect(page.locator('.chat-message.user').first()).toContainText('Say hi');
  await expect(page.locator('.chat-message.assistant .chat-markdown')).toContainText('Hello there');
  await expect(page.locator('.chat-wait-label')).toHaveCount(0);
  const answerBubble = page.locator('.chat-message.assistant .chat-answer-bubble').first();
  await expect(answerBubble).toBeVisible();
  expect(await answerBubble.evaluate((element) => getComputedStyle(element).backgroundColor)).not.toBe('rgba(0, 0, 0, 0)');
  expect(await answerBubble.evaluate((element) => parseFloat(getComputedStyle(element).borderTopLeftRadius))).toBeGreaterThan(0);
  await expect(page.getByRole('button', { name: 'Send' })).toBeVisible();
  expect(sent).toEqual({ content: 'Say hi', model: 'llama3.1:8b', stream: true, surface: 'Web' });
  expect(created.toolsEnabled).toBe(true);
  expect(created.workingDirectory).toBe('/tmp/project');
  await page.locator('.chat-info').first().hover();
  const info = page.locator('.chat-info-pop').first();
  await expect(info).toBeVisible();
  await expect(info).toContainText('Time to first token');
  await expect(info).toContainText('640 ms');
  await expect(info).toContainText('2 reasoning');
  await expect(info).toContainText('4.7');
  await expect(page.locator('.chat-message.assistant strong', { hasText: 'there' })).toBeVisible();
  await expect(page.locator('.chat-meta')).toContainText('This machine (ollama)');
  await expect(page.locator('.chat-tool')).toContainText('xeno_status');
  await page.locator('.chat-thinking button').click();
  await expect(page.locator('.chat-thinking-body')).toContainText('greeting');

  await page.getByTitle('Bad answer').click();
  await page.getByRole('dialog').locator('textarea[name="comment"]').fill('Too short');
  await page.getByRole('dialog').getByRole('button', { name: 'Save rating' }).click();
  await expect.poll(() => feedback).toEqual({ rating: -1, comment: 'Too short' });
  await expect(page.locator('.chat-feedback-note')).toHaveText('Too short');
  await expect(page.getByTitle('Bad answer')).toHaveAttribute('aria-pressed', 'true');

  await page.getByRole('button', { name: 'Chat history', exact: true }).click();
  await expect(page.locator('.metric-card', { hasText: 'Chat turns' })).toContainText('1');
  await expect(page.locator('.metric-card', { hasText: 'Thumbs down share' })).toContainText('100%');
  await expect(page.locator('.chat-activity-chart svg rect[data-series]')).toHaveCount(1);
  // The chart fills its panel and is 288px tall (270px of bars plus the time labels).
  const chartBox = await page.locator('.chat-activity-chart svg').boundingBox();
  const chartFrame = await page.locator('.chat-activity-chart').boundingBox();
  expect(Math.abs(chartBox.width - chartFrame.width)).toBeLessThanOrEqual(2);
  expect(Math.round(chartBox.height)).toBe(288);
  expect(analyticsBuckets.at(-1)).toBe('900');
  await page.getByRole('button', { name: 'Last Hour', exact: true }).click();
  await expect.poll(() => analyticsBuckets.at(-1)).toBe('60');
  await page.getByText('Say hi').first().click();
  await expect(page.getByRole('dialog').getByText('Chat turn')).toBeVisible();
  await page.getByRole('dialog').getByRole('button', { name: 'Open conversation' }).click();
  await expect(page.locator('.chat-header h2')).toHaveText('Say hi');

  await page.getByRole('button', { name: 'Feedback', exact: true }).click();
  await expect(page.getByText('Too short').first()).toBeVisible();
  await expect(page.locator('.metric-card', { hasText: 'Thumbs down' }).first()).toContainText('1');
  await page.locator('.data-table tbody tr', { hasText: 'Too short' }).first().click();
  const answer = page.getByRole('dialog').locator('.chat-excerpt');
  await expect(answer.locator('strong')).toHaveText('there');
  await expect(answer).not.toContainText('**');
});

test('usage page draws every chart, drills down, and handles empty and failed loads', async ({ page }) => {
  let mode = 'full';
  const respond = (dataset) => async (route) => {
    if (mode === 'error') {
      await route.fulfill({ status: 500, json: { Error: 'InternalServerError', Message: 'Usage is broken.' } });
      return;
    }
    await route.fulfill({ json: mode === 'empty' ? emptyUsage(dataset) : usageAnalytics(dataset) });
  };
  await page.route('**/api/v1/requests/analytics/usage**', respond('ServedRequests'));
  await page.route('**/api/v1/local-inference/analytics/usage**', respond('LocalEndpoint'));
  await page.route('**/api/v1/chat/analytics/usage**', respond('Chat'));
  await page.route('**/api/v1/chat/analytics?**', async (route) => route.fulfill({ json: { turns: 0, buckets: [], models: [] } }));
  const historyQueries = [];
  await page.route('**/api/v1/chat/history?**', async (route) => {
    historyQueries.push(new URL(route.request().url()).searchParams);
    await route.fulfill({ json: { items: [], total: 0, page: 1, pageSize: 500 } });
  });

  await page.goto('/index-local.html');
  const nav = await page.locator('.sidebar-link').allTextContents();
  expect(nav.indexOf('Usage')).toBeGreaterThanOrEqual(0);
  expect(nav.indexOf('Usage')).toBe(nav.indexOf('Request History') - 1);
  await page.getByRole('button', { name: 'Usage', exact: true }).click();
  await expect(page.locator('[data-chart]')).toHaveCount(11);
  for (const id of ['usage.outcome', 'usage.latency', 'usage.ttft', 'usage.streaming', 'usage.throughput', 'usage.tokens', 'usage.groups', 'usage.groupTotals', 'usage.heatmap', 'usage.stages', 'usage.stageAverages']) {
    await expect(page.locator(`[data-chart="${id}"] .usage-empty`)).toHaveCount(0);
  }
  await expect(page.locator('.metric-card').first()).toContainText('3');
  // A value with no neighbors still shows: one dot per percentile.
  await expect(page.locator('[data-chart="usage.latency"] circle.usage-point')).toHaveCount(4);
  await expect(page.locator('[data-chart="usage.throughput"] circle.usage-point')).toHaveCount(2);
  await expect(page.locator('[data-chart="usage.heatmap"] .usage-heat-cell.active')).toHaveCount(1);
  await expect(page.locator('[data-chart="usage.stageAverages"] .usage-bar-row')).toHaveCount(2);
  await expect(page.locator('.usage-groups table tbody tr')).toHaveCount(2);

  // Chat adds the feedback and tool charts.
  await page.getByRole('button', { name: 'Chat', exact: true }).last().click();
  await expect(page.locator('[data-chart]')).toHaveCount(13);
  await expect(page.locator('[data-chart="usage.feedback"]')).toContainText('Thumbs up');
  await expect(page.locator('[data-chart="usage.tools"]')).toContainText('Denied');

  // The tooltip is drawn on the body, so a card cannot clip it.
  const hover = page.locator('[data-chart="usage.outcome"] .usage-hover').nth(30);
  await hover.hover();
  await expect(page.locator('body > .usage-chart-tooltip')).toBeVisible();
  await expect(page.locator('body > .usage-chart-tooltip')).toContainText('Failed');

  // Clicking a bucket opens Chat history filtered to it, and the banner clears it.
  await hover.click();
  await expect(page.locator('.usage-drill-banner')).toContainText('from the Usage page');
  await expect.poll(() => historyQueries.at(-1)?.get('fromUtc') || '').not.toBe('');
  const window = historyQueries.at(-1);
  expect(new Date(window.get('toUtc')).getTime() - new Date(window.get('fromUtc')).getTime()).toBe(60000);
  await page.locator('.usage-drill-banner button').click();
  await expect(page.locator('.usage-drill-banner')).toHaveCount(0);

  // Empty and error states.
  await page.getByRole('button', { name: 'Usage', exact: true }).click();
  mode = 'empty';
  await page.getByRole('button', { name: 'Refresh' }).first().click();
  await expect(page.locator('.usage-empty-page')).toContainText('No traffic retained');
  await expect(page.locator('[data-chart="usage.latency"] .usage-empty')).toContainText('No requests with timing');
  mode = 'error';
  await page.getByRole('button', { name: 'Refresh' }).first().click();
  await expect(page.locator('.error-state')).toContainText('Usage is broken.');
  mode = 'full';
  await page.getByRole('button', { name: 'Try again' }).click();
  await expect(page.locator('[data-chart]')).toHaveCount(11);
});

test('usage page fits every width and theme', async ({ page }) => {
  await page.route('**/api/v1/requests/analytics/usage**', async (route) => route.fulfill({ json: usageAnalytics('ServedRequests') }));
  for (const width of [1280, 768, 390]) {
    for (const theme of ['light', 'dark']) {
      await page.setViewportSize({ width, height: 900 });
      await page.goto('/index-local.html');
      if (width <= 860) {
        await page.locator('.sidebar-link', { hasText: 'Usage' }).first().evaluate((element) => element.click());
      } else {
        await page.getByRole('button', { name: 'Usage', exact: true }).click();
      }
      await expect(page.locator('[data-chart]')).toHaveCount(11);
      await page.evaluate((value) => document.documentElement.setAttribute('data-theme', value), theme);
      await page.waitForTimeout(150);
      const overflow = await page.evaluate(() => document.documentElement.scrollWidth - window.innerWidth);
      expect(overflow, `horizontal overflow at ${width} ${theme}`).toBeLessThanOrEqual(1);
      const tile = await page.locator('.usage-kpis .metric-card').first().boundingBox();
      expect(tile.width, `KPI tile too narrow at ${width}`).toBeGreaterThanOrEqual(140);
      await page.screenshot({ fullPage: true, path: `test-results/usage-local-${width}-${theme}.png` });
    }
  }
});

test('usage page keeps its layout with longer translated text', async ({ page }) => {
  await page.route('**/api/v1/requests/analytics/usage**', async (route) => route.fulfill({ json: usageAnalytics('ServedRequests') }));
  await page.setViewportSize({ width: 768, height: 900 });
  await page.goto('/index-local.html');
  await page.locator('.language-selector select, select[aria-label="Language"], .topbar select').first().selectOption('es');
  await page.locator('.sidebar-link', { hasText: 'Uso' }).first().evaluate((element) => element.click());
  await expect(page.locator('[data-chart="usage.stages"] h2')).toHaveText('Tiempo por etapa');
  await expect(page.locator('[data-chart="usage.outcome"] .chart-legend')).toContainText('Respondidas por respaldo de red');
  expect(await page.evaluate(() => document.documentElement.scrollWidth - window.innerWidth)).toBeLessThanOrEqual(1);
});

test('provider telemetry shows the health strip, and request details show timing bars', async ({ page }) => {
  await page.route('**/api/v1/telemetry/providers/health**', async (route) => route.fulfill({ json: healthStrip() }));
  await page.route('**/api/v1/telemetry/providers/latest**', async (route) => route.fulfill({ json: { snapshot: { providers: [] }, detail: { sample: {}, models: [] } } }));
  await page.route('**/api/v1/telemetry/providers/history/search**', async (route) => route.fulfill({ json: { total: 0, samples: [] } }));
  await page.route('**/api/v1/telemetry/providers/history/rollups**', async (route) => route.fulfill({ json: [] }));
  await page.route('**/api/v1/requests/req_local_1', async (route) => route.fulfill({ json: { ...requestEntry, telemetry: { durationMs: 1000, timeToHeadersMs: 100, timeToFirstByteMs: 300 } } }));
  await page.goto('/index-local.html');
  await page.locator('.sidebar-link', { hasText: 'Provider' }).last().click();
  const strip = page.locator('[data-chart="usage.health"]');
  await expect(strip.locator('.usage-strip-row')).toHaveCount(1);
  await expect(strip).toContainText('75% up');
  for (const status of ['Healthy', 'Degraded', 'Unhealthy', 'NoData']) await expect(strip.locator(`.usage-strip-cells [data-status="${status}"]`)).toHaveCount(1);
  await expect(strip.locator('.usage-strip-cells [data-status="Unhealthy"]')).toHaveText('×');

  await page.getByRole('button', { name: 'Request History', exact: true }).click();
  await page.locator('tbody tr').first().click();
  await expect(page.getByText('Where the time went')).toBeVisible();
  await expect(page.locator('.usage-timing .usage-bar-row')).toHaveCount(3);
  await expect(page.locator('.usage-timing')).toContainText('Provider to headers');
});
