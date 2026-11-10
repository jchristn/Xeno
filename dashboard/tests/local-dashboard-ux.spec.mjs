import { expect, test } from '@playwright/test';

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
  await expect(page.locator('.telemetry-collapsible-chart')).toHaveCount(3);
  await expect(page.getByRole('heading', { name: 'CPU Telemetry' })).toBeVisible();
  await expect(page.getByRole('heading', { name: 'Memory Telemetry' })).toBeVisible();
  await expect(page.getByRole('heading', { name: 'GPU Telemetry' })).toBeVisible();
  await expect(page.locator('.telemetry-spark-chart-panel .activity-chart')).toBeVisible();

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
