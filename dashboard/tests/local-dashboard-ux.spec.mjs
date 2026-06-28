import { expect, test } from '@playwright/test';

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

test.beforeEach(async ({ page }) => {
  await page.route('**/api/v1/state', async (route) => route.fulfill({ json: stateResponse }));
  await page.route('**/api/v1/settings', async (route) => route.fulfill({ json: stateResponse }));
  await page.route('**/api/v1/statistics', async (route) => route.fulfill({ json: { requests: 12, failures: 1, streamingRequests: 3, averageLatencyMs: 42 } }));
  await page.route('**/api/v1/requests/summary**', async (route) => route.fulfill({ json: { buckets: [{ startUtc: '2026-06-26T08:00:00Z', total: 4, success: 4, failure: 0 }, { startUtc: '2026-06-26T09:00:00Z', total: 8, success: 7, failure: 1 }] } }));
  await page.route('**/api/v1/providers*/test', async (route) => route.fulfill({ json: { name: 'local-ollama', endpoint: 'http://127.0.0.1:11434', success: true, statusCode: 200 } }));
  await page.route('**/api/v1/providers**', async (route) => route.fulfill({ json: route.request().method() === 'GET' ? providersResponse : { removed: 1, removedOne: true } }));
  await page.route('**/api/v1/models**', async (route) => route.fulfill({ json: route.request().method() === 'GET' ? modelsResponse : { removed: 1, removedOne: true } }));
  await page.route('**/api/v1/requests/req_local_1', async (route) => route.fulfill({ json: requestEntry }));
  await page.route('**/api/v1/requests/bulk', async (route) => route.fulfill({ json: { removed: 1, removedOne: true } }));
  await page.route('**/api/v1/requests', async (route) => route.fulfill({ json: { entries: [requestEntry] } }));
  await page.route('**/api/v1/logs**', async (route) => route.fulfill({ json: { logFile: 'xeno.log', lines: ['2026-06-26 08:00:00.000000Z [INFO] Local dashboard listening.'] } }));
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
  await expect(page.getByRole('heading', { name: 'Home' })).toBeVisible();
  await expect(page.locator('.activity-chart')).toBeVisible();
  expect(await page.locator('.activity-chart rect').count()).toBeGreaterThan(0);
  await expect(page.getByRole('button', { name: 'Last Hour' })).toBeVisible();
  await expect(page.locator('.topbar-server-url')).toContainText('http://127.0.0.1:9343');

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
  await expect(page.getByText('Response Body')).toBeVisible();
  await page.getByTitle('Close this modal without applying unsaved changes.').click();

  await page.getByRole('button', { name: 'Logs', exact: true }).click();
  await expect(page.getByText('[INFO] Local dashboard listening.')).toBeVisible();

  await page.getByRole('button', { name: 'Settings', exact: true }).click();
  await expect(page.getByRole('heading', { name: 'Database Status' })).toBeVisible();

  const missingTooltips = await page.locator('button, a, input, select').evaluateAll((nodes) => nodes
    .filter((node) => !node.getAttribute('title') && !node.getAttribute('aria-label'))
    .map((node) => node.outerHTML));
  expect(missingTooltips).toEqual([]);
});
