import { expect, test } from '@playwright/test';
import fs from 'node:fs/promises';
import { healthStrip, usageAnalytics } from './usageFixture.mjs';

// The usage page shows one chart at a time; tests pick it from the Chart list.
const usageChartOptions = (page) => page.locator('.usage-chart-picker select option');
async function showChart(page, id) {
  await page.locator('.usage-chart-picker select').selectOption(id);
  await expect(page.locator(`[data-chart="${id}"]`)).toHaveCount(1);
}

const serverUrl = 'http://localhost:9000';

function session(userId) {
  return {
    token: 'token-123',
    tenantId: 'ten_default',
    tenantName: 'Default',
    serverUrl,
    userId,
    email: `${userId}@xeno.test`,
    username: `${userId}@xeno.test`
  };
}

async function routeDashboardApis(page, users, onUpdate) {
  await page.route(`${serverUrl}/**`, async (route) => {
    const url = new URL(route.request().url());
    if (url.pathname === '/api/v1/users' && route.request().method() === 'GET') {
      await route.fulfill({ json: users });
      return;
    }

    if (url.pathname.startsWith('/api/v1/users/') && route.request().method() === 'PUT') {
      const body = route.request().postDataJSON();
      const id = decodeURIComponent(url.pathname.substring('/api/v1/users/'.length));
      onUpdate?.(body);
      await route.fulfill({
        json: {
          ...users.find((item) => item.id === id),
          ...body
        }
      });
      return;
    }

    await route.fulfill({ json: [] });
  });
}

async function openUsers(page, userId, users, onUpdate) {
  await page.addInitScript((storedSession) => {
    window.localStorage.setItem('xeno.dashboard.session', JSON.stringify(storedSession));
  }, session(userId));
  await routeDashboardApis(page, users, onUpdate);
  await page.goto('/dashboard/users');
  await expect(page.locator('h1', { hasText: 'Users' })).toBeVisible();
}

async function openRowActions(row) {
  await row.getByRole('button', { name: 'Row Actions' }).click();
}

async function routeNukeEndpoints(page, onNuke) {
  await page.route(`${serverUrl}/v1.0/api/admin/nuke/**`, async (route) => {
    const url = new URL(route.request().url());
    const body = route.request().postDataJSON();
    const isTenant = url.pathname.endsWith('/tenant');
    onNuke?.(isTenant ? 'tenant' : 'user', body);
    await route.fulfill({
      json: isTenant ? {
        status: 'success',
        operationId: 'row_nuke_tenant_result',
        operationType: 'TenantNuke',
        dryRun: false,
        tenant: { tenantId: body.tenantId, name: 'Operations', status: 'Deleted' },
        deleted: { tenants: 1, users: 1 },
        updated: {},
        warnings: []
      } : {
        status: 'success',
        operationId: 'row_nuke_user_result',
        operationType: 'UserNuke',
        dryRun: false,
        tenant: { tenantId: body.tenantId, name: 'Default', status: 'Active' },
        user: { userId: body.userId, email: 'target@xeno.test', status: 'Deleted' },
        deleted: { users: 1 },
        updated: { requestHistoryAnonymized: 1 },
        warnings: []
      }
    });
  });
}

async function openSecurity(page, fixtures, onCreateRoleAssignment) {
  await page.addInitScript((storedSession) => {
    window.localStorage.setItem('xeno.dashboard.session', JSON.stringify(storedSession));
  }, session('usr_tenant_admin'));
  await page.route(`${serverUrl}/**`, async (route) => {
    const url = new URL(route.request().url());
    const method = route.request().method();
    if (url.pathname === '/api/v1/roles' && method === 'GET') {
      await route.fulfill({ json: fixtures.roles || [] });
      return;
    }

    if (url.pathname === '/api/v1/permissions' && method === 'GET') {
      await route.fulfill({ json: fixtures.permissions || [] });
      return;
    }

    if (url.pathname === '/api/v1/permissions/grantable' && method === 'GET') {
      await route.fulfill({ json: fixtures.grantablePermissions || [] });
      return;
    }

    if (url.pathname === '/api/v1/roleassignments' && method === 'GET') {
      await route.fulfill({ json: fixtures.roleAssignments || [] });
      return;
    }

    if (url.pathname === '/api/v1/roleassignments' && method === 'POST') {
      const body = route.request().postDataJSON();
      onCreateRoleAssignment?.(body);
      await route.fulfill({ json: { id: 'rassn_created', tenantId: 'ten_default', ...body } });
      return;
    }

    if (url.pathname === '/api/v1/users' && method === 'GET') {
      const fixtureUsers = fixtures.users || [];
      const hasActor = fixtureUsers.some((user) => user.id === 'usr_tenant_admin');
      await route.fulfill({
        json: hasActor
          ? fixtureUsers
          : [
            { id: 'usr_tenant_admin', tenantId: 'ten_default', email: 'tenant-admin@xeno.test', username: 'tenant-admin@xeno.test', isAdmin: false, isTenantAdmin: true, active: true },
            ...fixtureUsers
          ]
      });
      return;
    }

    if (url.pathname === '/api/v1/credentials' && method === 'GET') {
      await route.fulfill({ json: fixtures.credentials || [] });
      return;
    }

    if (url.pathname === '/api/v1/effective-permissions' && method === 'GET') {
      await route.fulfill({ json: fixtures.effectivePermissions || [] });
      return;
    }

    if (url.pathname === '/api/v1/audit' && method === 'GET') {
      await route.fulfill({ json: fixtures.audit || [] });
      return;
    }

    await route.fulfill({ json: [] });
  });
  await page.goto('/dashboard/security');
  await expect(page.locator('h1', { hasText: 'Security' })).toBeVisible();
}

async function openLedger(page, user, ledgerFixtures = {}) {
  await page.addInitScript((storedSession) => {
    window.localStorage.setItem('xeno.dashboard.session', JSON.stringify(storedSession));
  }, session(user.id));
  await page.route(`${serverUrl}/**`, async (route) => {
    const url = new URL(route.request().url());
    const method = route.request().method();
    if (url.pathname === '/api/v1/users' && method === 'GET') {
      await route.fulfill({ json: ledgerFixtures.users || [user] });
      return;
    }

    if (url.pathname === '/api/v1/tenants' && method === 'GET') {
      await route.fulfill({ json: ledgerFixtures.tenants || [{ id: user.tenantId || 'ten_default', name: 'Default', active: true }] });
      return;
    }

    if (url.pathname === '/v1.0/api/ledger/status' && method === 'GET') {
      if (ledgerFixtures.statusError) {
        await route.fulfill({ json: { queue: { enabled: true, queueDepth: 1, processedCount: 0 }, netLedgerHealthy: false, netLedgerMessage: 'NetLedger is unavailable.' } });
        return;
      }

      await route.fulfill({
        json: ledgerFixtures.status || {
          queue: { enabled: true, queueDepth: 0, processedCount: 4, retryCount: 0 },
          netLedgerHealthy: true,
          netLedgerMessage: ''
        }
      });
      return;
    }

    if (url.pathname === '/v1.0/api/ledger/balance/me' && method === 'GET') {
      await route.fulfill({ json: ledgerFixtures.me || { xenoTenantId: user.tenantId || 'ten_default', userId: user.id, balance: 70, totalDebits: 30, totalCredits: 100 } });
      return;
    }

    if (url.pathname === '/v1.0/api/ledger/balances' && method === 'GET') {
      await route.fulfill({
        json: ledgerFixtures.balances || {
          items: [
            { xenoTenantId: 'ten_default', userId: user.id, balance: 70, totalDebits: 30, totalCredits: 100, lastActivityUtc: '2026-07-16T12:00:00Z' },
            { xenoTenantId: 'ten_default', userId: 'usr_peer', balance: -30, totalDebits: 80, totalCredits: 50, lastActivityUtc: '2026-07-16T11:00:00Z' }
          ],
          total: 2,
          page: 1,
          pageSize: 100
        }
      });
      return;
    }

    if (url.pathname === '/v1.0/api/ledger/entries/me' && method === 'GET') {
      await route.fulfill({
        json: ledgerFixtures.entries || {
          items: [
            { ledgerEventId: 'ledevt_1', xenoTenantId: 'ten_default', userId: user.id, direction: 'Debit', amount: 30, counterpartyUserId: 'usr_peer', selectedClientId: 'xcl_peer', requestHistoryId: 'req_ledger_1', occurredUtc: '2026-07-16T12:00:00Z' },
            { ledgerEventId: 'ledevt_2', xenoTenantId: 'ten_default', userId: user.id, servingUserId: user.id, direction: 'Credit', amount: 100, counterpartyUserId: 'usr_peer', selectedClientId: 'xcl_user', requestHistoryId: 'req_ledger_2', occurredUtc: '2026-07-16T12:10:00Z' }
          ],
          total: 2
        }
      });
      return;
    }

    if (url.pathname.startsWith('/v1.0/api/request-history/') && method === 'GET') {
      const requestHistoryId = decodeURIComponent(url.pathname.substring('/v1.0/api/request-history/'.length));
      await route.fulfill({
        json: ledgerFixtures.requestHistory || {
          id: requestHistoryId,
          tenantId: user.tenantId || 'ten_default',
          method: 'POST',
          path: '/v1/chat/completions',
          model: 'ledger-model',
          statusCode: 200,
          selectedClientId: 'xcl_peer',
          createdUtc: '2026-07-16T12:00:00Z',
          routingDecision: { selectedClientId: 'xcl_peer', outcomeCode: 'Allowed' },
          telemetry: { promptTokens: 10, outputTokens: 20, totalTokens: 30 },
          timeline: []
        }
      });
      return;
    }

    if (url.pathname === '/v1.0/api/ledger/drain' && method === 'POST') {
      await route.fulfill({ json: { processed: 1, retried: 0, terminalFailures: 0 } });
      return;
    }

    if (url.pathname === '/v1.0/api/ledger/reconcile' && method === 'POST') {
      await route.fulfill({ json: { missingTenantLinks: [], missingAccountLinks: [] } });
      return;
    }

    await route.fulfill({ json: [] });
  });
  await page.goto('/dashboard/ledger');
  await expect(page.locator('h1', { hasText: 'Ledger' })).toBeVisible();
}

async function openSettings(page, onSaveSettings) {
  const user = { id: 'usr_admin', tenantId: 'ten_default', email: 'admin@xeno.test', isAdmin: true, isTenantAdmin: false, active: true };
  const settings = {
    Ledger: {
      Enabled: false,
      BaseUrl: 'http://localhost:8080',
      ApiKey: '',
      SecretKey: '',
      DrainIntervalMs: 1000,
      MaxBatchSize: 100,
      MaxQueueDepth: 10000,
      RequestTimeoutMs: 30000,
      CreateTenantsAutomatically: true,
      CreateAccountsAutomatically: true,
      CommitEntriesImmediately: true
    }
  };

  await page.addInitScript((storedSession) => {
    window.localStorage.setItem('xeno.dashboard.session', JSON.stringify(storedSession));
  }, session(user.id));
  await page.route(`${serverUrl}/**`, async (route) => {
    const url = new URL(route.request().url());
    const method = route.request().method();
    if (url.pathname === '/api/v1/users' && method === 'GET') {
      await route.fulfill({ json: [user] });
      return;
    }

    if (url.pathname === '/api/v1/settings' && method === 'GET') {
      await route.fulfill({ json: settings });
      return;
    }

    if (url.pathname === '/api/v1/settings' && method === 'POST') {
      const body = route.request().postDataJSON();
      onSaveSettings?.(body);
      await route.fulfill({ json: body });
      return;
    }

    await route.fulfill({ json: [] });
  });
  await page.goto('/dashboard/settings');
  await expect(page.locator('h1', { hasText: 'Settings' })).toBeVisible();
}

async function openLoginWithTenantLookup(page, tenants) {
  await page.route(`${serverUrl}/api/v1/auth/tenants`, async (route) => {
    await route.fulfill({ json: { tenants } });
  });
  await page.goto('/');
  await page.getByRole('textbox', { name: 'Server URL' }).fill(serverUrl);
  await page.getByRole('textbox', { name: 'Email' }).fill('operator@xeno.test');
  await page.getByRole('textbox', { name: 'Email' }).press('Enter');
}

async function fulfillLeaderboard(route) {
  await route.fulfill({
    json: {
      models: {
        'llama3.2:latest': { requestCount: 2, totalTokens: 130, promptTokens: 50, outputTokens: 80 },
        'mistral:latest': { requestCount: 1, totalTokens: 60, promptTokens: 25, outputTokens: 35 }
      },
      total: 2,
      limit: 10,
      offset: 0
    }
  });
}

async function openApiExplorer(page) {
  await page.addInitScript((storedSession) => {
    window.localStorage.setItem('xeno.dashboard.session', JSON.stringify(storedSession));
  }, session('usr_admin'));
  await page.route(`${serverUrl}/**`, async (route) => {
    const url = new URL(route.request().url());
    const method = route.request().method();
    if (url.pathname === '/api/v1/users' && method === 'GET') {
      await route.fulfill({ json: [{ id: 'usr_admin', tenantId: 'ten_default', email: 'admin@xeno.test', isAdmin: true, isTenantAdmin: false, active: true }] });
      return;
    }

    if (url.pathname === '/openapi.json' && method === 'GET') {
      await route.fulfill({
        json: {
          openapi: '3.0.1',
          paths: {
            '/v1/chat/completions': {
              post: {
                operationId: 'createChatCompletion',
                tags: ['OpenAI'],
                summary: 'Create chat completion',
                description: 'OpenAI-compatible chat completion.'
              }
            }
          }
        }
      });
      return;
    }

    if (url.pathname === '/v1/models' && method === 'GET') {
      await route.fulfill({ json: { data: [{ id: 'gpt-oss:120b' }] } });
      return;
    }

    if (url.pathname === '/v1/chat/completions' && method === 'POST') {
      const body = route.request().postDataJSON();
      if (body.stream === true) {
        await route.fulfill({
          body: 'data: {"choices":[{"delta":{"content":"hello"}}]}\n\ndata: [DONE]\n\n',
          headers: { 'content-type': 'text/event-stream' },
          status: 200
        });
        return;
      }

      await route.fulfill({
        json: {
          choices: [
            { message: { content: 'hello' } }
          ]
        }
      });
      return;
    }

    await route.fulfill({ json: [] });
  });
  await page.goto('/dashboard/explorer');
  await expect(page.locator('h1', { hasText: 'API Explorer' })).toBeVisible();
}

test('api explorer labels first response timing as first token only for streamed responses', async ({ page }) => {
  await openApiExplorer(page);

  const requestBody = page.locator('textarea.explorer-json-editor').nth(1);
  await expect(requestBody).toHaveValue(/"stream": true/);
  await page.getByRole('button', { name: 'Execute' }).click();

  const indicators = page.locator('.api-explorer-response-indicators');
  await expect(indicators).toContainText('Server-Sent Events');
  await expect(indicators).toContainText('Time to First Token');
  await expect(indicators).not.toContainText('Time To First Byte');

  const currentBody = JSON.parse(await requestBody.inputValue());
  await requestBody.fill(JSON.stringify({ ...currentBody, stream: false }, null, 2));
  await page.getByRole('button', { name: 'Execute' }).click();

  await expect(indicators).toContainText('Standard');
  await expect(indicators).toContainText('Time To First Byte');
  await expect(indicators).not.toContainText('Time to First Token');
});

test('login focuses tenant selector after email submit when multiple tenants are available', async ({ page }) => {
  await openLoginWithTenantLookup(page, [
    { id: 'ten_default', name: 'Default' },
    { id: 'ten_ops', name: 'Operations' }
  ]);

  await expect(page.getByRole('combobox', { name: 'Tenant' })).toBeFocused();
});

test('login focuses password after email submit when one tenant is available', async ({ page }) => {
  await openLoginWithTenantLookup(page, [
    { id: 'ten_default', name: 'Default' }
  ]);

  await expect(page.getByLabel('Password')).toBeFocused();
});

test('login tenant selector stays within the login card for long tenant names', async ({ page }) => {
  await openLoginWithTenantLookup(page, [
    { id: 'ten_default', name: 'Default' },
    { id: 'ten_extremely_long_identifier_for_operations_department', name: 'Operations Department With A Very Long Organization Name That Should Not Stretch The Card' }
  ]);

  const cardWidth = await page.locator('.login-card').evaluate((element) => element.getBoundingClientRect().width);
  const selectorWidth = await page.getByRole('combobox', { name: 'Tenant' }).evaluate((element) => element.getBoundingClientRect().width);
  expect(selectorWidth).toBeLessThanOrEqual(cardWidth);
});

test('register existing organization omits username and stays in dashboard after success', async ({ page }) => {
  await page.route(`${serverUrl}/**`, async (route) => {
    const url = new URL(route.request().url());
    if (url.pathname === '/v1.0/api/onboarding/signup') {
      const body = route.request().postDataJSON();
      expect(body).not.toHaveProperty('username');
      await route.fulfill({
        json: {
          status: 'success',
          tenant: { id: 'ten_existing', name: 'Existing Organization' },
          user: { id: 'usr_registered', email: 'new-user@xeno.test', username: 'new-user@xeno.test' },
          credential: { id: 'cred_registered', accessKey: 'ak_registered', secretKey: 'sk_registered' },
          token: { token: 'registered-token', tokenType: 'Bearer', sessionId: 'sess_registered', expiresUtc: '2026-07-08T12:00:00Z' }
        }
      });
      return;
    }

    await route.fulfill({
      status: 403,
      json: { error: 'PermissionDenied', message: 'The registered user does not have this dashboard permission.' }
    });
  });

  await page.goto('/register');
  await page.getByRole('radio', { name: 'I am joining an existing organization' }).click();
  await expect(page.getByText('Username Optional')).toHaveCount(0);
  await page.getByLabel('Tenant ID').fill('ten_existing');
  await page.getByLabel('Email').fill('new-user@xeno.test');
  await page.getByLabel('Password').fill('password123');
  await page.getByRole('button', { name: 'Create Account' }).click();
  const dialog = page.getByRole('dialog');
  await expect(dialog.getByRole('heading', { name: 'Registration Complete' })).toBeVisible();
  await expect(dialog.getByText('Welcome to Xeno! We have created your account.')).toBeVisible();
  await expect(dialog.getByRole('columnheader', { name: 'Property' })).toBeVisible();
  await expect(dialog.getByText('ten_existing')).toBeVisible();
  await expect(dialog.getByText('Existing Organization')).toBeVisible();
  await expect(dialog.getByText('cred_registered')).toHaveCount(2);
  await expect(dialog.getByText('ak_registered')).toBeVisible();
  await expect(dialog.getByText('sk_registered')).toBeVisible();
  const [download] = await Promise.all([
    page.waitForEvent('download'),
    dialog.getByRole('button', { name: 'Download JSON' }).click()
  ]);
  expect(download.suggestedFilename()).toBe('xeno-registration-credentials.json');
  const downloadPath = await download.path();
  expect(downloadPath).toBeTruthy();
  const downloadedJson = JSON.parse(await fs.readFile(downloadPath, 'utf8'));
  expect(downloadedJson).toEqual({
    tenant: {
      id: 'ten_existing',
      name: 'Existing Organization'
    },
    user: {
      id: 'usr_registered',
      email: 'new-user@xeno.test'
    },
    credential: {
      accessKey: 'ak_registered',
      secretKey: 'sk_registered'
    }
  });
  await dialog.getByRole('button', { name: 'Dashboard' }).click();
  await expect(page).toHaveURL(/\/dashboard\/home$/);
  await page.waitForTimeout(250);
  await expect(page).toHaveURL(/\/dashboard\/home$/);
  await expect(page.getByRole('heading', { name: 'Sign In To Dashboard' })).toHaveCount(0);
});

test('register route clears an existing dashboard session before onboarding', async ({ page }) => {
  await page.addInitScript((storedSession) => {
    window.localStorage.setItem('xeno.dashboard.session', JSON.stringify(storedSession));
  }, session('usr_existing'));

  await page.goto('/register');

  await expect(page).toHaveURL(/\/register$/);
  await expect(page.getByRole('heading', { name: 'Set up your dashboard account' })).toBeVisible();
  await expect(page.getByRole('radio', { name: 'I am creating a new organization' })).toBeVisible();
  await expect(page.getByRole('heading', { name: 'Sign In To Dashboard' })).toHaveCount(0);
  await expect.poll(async () => page.evaluate(() => window.localStorage.getItem('xeno.dashboard.session'))).toBeNull();
});

test('register errors render only under their matching fields', async ({ page }) => {
  await page.route(`${serverUrl}/v1.0/api/onboarding/signup`, async (route) => {
    const body = route.request().postDataJSON();
    if (body.tenantId === 'ten_missing') {
      await route.fulfill({
        status: 404,
        json: { error: 'TenantNotFound', message: 'Tenant ID was not found.' }
      });
      return;
    }

    await route.fulfill({
      status: 409,
      json: { error: 'GlobalAdminEmailAlreadyExists', message: 'Email already belongs to a global administrator.' }
    });
  });

  await page.goto('/register');
  await page.getByRole('radio', { name: 'I am joining an existing organization' }).click();
  await page.getByLabel('Tenant ID').fill('ten_missing');
  await page.getByLabel('Email').fill('member@xeno.test');
  await page.getByLabel('Password').fill('password123');
  await page.getByRole('button', { name: 'Create Account' }).click();
  await expect(page.getByText('That tenant ID could not be found.')).toHaveCount(1);
  await expect(page.getByRole('alert')).toHaveCount(0);

  await page.getByLabel('Tenant ID').fill('ten_default');
  await page.getByLabel('Email').fill('admin@xeno.test');
  await page.getByRole('button', { name: 'Create Account' }).click();
  await expect(page.getByText('Email already belongs to a global administrator.')).toHaveCount(1);
  await expect(page.getByText('That tenant ID could not be found.')).toHaveCount(0);
  await expect(page.getByRole('alert')).toHaveCount(0);
});

test('unknown public routes render the styled not found page', async ({ page }) => {
  await page.goto('/not-a-real-dashboard-page');

  await expect(page.getByText('404')).toBeVisible();
  await expect(page.getByText("We're sorry, but the page you requested could not be found.")).toBeVisible();
});

test('unknown authenticated dashboard routes render the styled not found page', async ({ page }) => {
  await page.addInitScript((storedSession) => {
    window.localStorage.setItem('xeno.dashboard.session', JSON.stringify(storedSession));
  }, session('usr_regular'));

  await page.goto('/dashboard/not-a-real-page');

  await expect(page.locator('h1', { hasText: 'Page Not Found' })).toBeVisible();
  await expect(page.getByText('404')).toBeVisible();
  await expect(page.getByText("We're sorry, but the page you requested could not be found.")).toBeVisible();
});

test('danger zone route is removed from authenticated navigation', async ({ page }) => {
  await page.addInitScript((storedSession) => {
    window.localStorage.setItem('xeno.dashboard.session', JSON.stringify(storedSession));
  }, session('usr_admin'));
  await page.route(`${serverUrl}/**`, async (route) => {
    const url = new URL(route.request().url());
    if (url.pathname === '/api/v1/users') {
      await route.fulfill({ json: [{ id: 'usr_admin', tenantId: 'ten_default', email: 'admin@xeno.test', isAdmin: true, isTenantAdmin: false }] });
      return;
    }

    await route.fulfill({ json: [] });
  });

  await page.goto('/dashboard/danger-zone');

  await expect(page.getByRole('link', { name: 'Danger Zone' })).toHaveCount(0);
  await expect(page.locator('h1', { hasText: 'Page Not Found' })).toBeVisible();
  await expect(page.getByText('404')).toBeVisible();
});

test('audit logs are a top-level admin page outside security', async ({ page }) => {
  await page.addInitScript((storedSession) => {
    window.localStorage.setItem('xeno.dashboard.session', JSON.stringify(storedSession));
  }, session('usr_admin'));
  await page.route(`${serverUrl}/**`, async (route) => {
    const url = new URL(route.request().url());
    const method = route.request().method();
    if (url.pathname === '/api/v1/users' && method === 'GET') {
      await route.fulfill({ json: [{ id: 'usr_admin', tenantId: 'ten_default', email: 'admin@xeno.test', isAdmin: true, isTenantAdmin: false }] });
      return;
    }

    if (url.pathname === '/api/v1/roles' && method === 'GET') {
      await route.fulfill({ json: [{ id: 'role_admin', tenantId: 'ten_default', name: 'SystemAdmin', builtIn: true, active: true }] });
      return;
    }

    if (url.pathname === '/api/v1/permissions' && method === 'GET') {
      await route.fulfill({ json: [{ id: 'perm_security_read', tenantId: 'ten_default', resourceType: 'Security', operationType: 'Read', permit: true, builtIn: true, active: true }] });
      return;
    }

    if (url.pathname === '/api/v1/effective-permissions' && method === 'GET') {
      await route.fulfill({ json: [{ resourceType: 'Security', operationType: 'Read', permit: true, sourceRole: 'SystemAdmin' }] });
      return;
    }

    if (url.pathname === '/api/v1/audit' && method === 'GET') {
      await route.fulfill({
        json: [{
          id: 'audit_admin_login',
          createdUtc: '2026-07-08T12:34:56Z',
          resourceType: 'Users',
          operationType: 'Read',
          reasonCode: 'Allowed',
          statusCode: 200,
          principalType: 'User',
          principalId: 'usr_admin',
          targetTenantId: 'ten_default'
        }]
      });
      return;
    }

    await route.fulfill({ json: [] });
  });

  await page.goto('/dashboard/security');
  await expect(page.locator('h1', { hasText: 'Security' })).toBeVisible();
  const administrationLinks = await page.locator('.sidebar-group').filter({ hasText: 'Administration' }).locator('.sidebar-link span').allTextContents();
  expect(administrationLinks.slice(administrationLinks.indexOf('Security'), administrationLinks.indexOf('Security') + 2)).toEqual(['Security', 'Audit Logs']);
  await expect(page.locator('main').getByRole('heading', { name: 'Audit Logs' })).toHaveCount(0);

  await page.getByRole('link', { name: 'Audit Logs' }).click();
  await expect(page.locator('h1', { hasText: 'Audit Logs' })).toBeVisible();
  await expect(page.getByText('2026-07-08T12:34:56Z')).toBeVisible();
  await expect(page.getByRole('columnheader', { name: 'Target Tenant ID' })).toHaveCount(0);
  const createdUtcHeader = page.getByRole('columnheader', { name: 'Created UTC' }).first();
  await expect(createdUtcHeader).toBeVisible();
  await expect(createdUtcHeader).toHaveCSS('white-space', 'nowrap');
});

test('ledger page renders system admin controls and summaries', async ({ page }) => {
  await openLedger(page, { id: 'usr_admin', tenantId: 'ten_default', email: 'admin@xeno.test', isAdmin: true, isTenantAdmin: false, active: true }, {
    tenants: [
      { id: 'ten_default', name: 'Default', active: true },
      { id: 'ten_other', name: 'Other', active: true }
    ],
    users: [
      { id: 'usr_admin', tenantId: 'ten_default', email: 'admin@xeno.test', isAdmin: true, isTenantAdmin: false, active: true },
      { id: 'usr_other', tenantId: 'ten_other', email: 'other@xeno.test', active: true }
    ],
    entries: {
      items: [
        { ledgerEventId: 'ledevt_1', tenantId: 'ten_default', direction: 'Debit', amount: 30, counterpartyUserId: 'usr_peer', selectedClientId: 'xcl_peer', requestHistoryId: 'req_ledger_1', occurredUtc: '2026-07-16T12:00:00Z', tags: { requestUserId: 'usr_admin', servingUserId: 'usr_peer' } },
        { ledgerEventId: 'ledevt_2', tenantId: 'ten_default', direction: 'Credit', amount: 100, selectedClientId: 'xcl_user', requestHistoryId: 'req_ledger_2', occurredUtc: '2026-07-16T12:10:00Z', tags: { requestUserId: 'usr_peer', servingUserId: 'usr_admin', counterpartyUserId: 'usr_peer' } },
        { ledgerEventId: 'ledevt_3', tenantId: 'ten_other', direction: 'Credit', amount: 60, selectedClientId: 'xcl_other', requestHistoryId: 'req_ledger_3', occurredUtc: '2026-07-16T12:20:00Z', tags: { requestUserId: 'usr_admin', servingUserId: 'usr_other', counterpartyUserId: 'usr_admin' } }
      ],
      total: 3
    }
  });

  // The NetLedger console link lives under Settings > Related Consoles, not on the ledger page.
  await expect(page.getByRole('link', { name: 'NetLedger UI' })).toHaveCount(0);
  await expect(page.getByRole('button', { name: 'Drain' })).toBeVisible();
  await expect(page.getByRole('button', { name: 'Reconcile' })).toBeVisible();
  await expect(page.getByRole('heading', { name: 'Tenant Ledger Summary' })).toBeVisible();
  await expect(page.getByRole('heading', { name: 'Top Requesting Users' })).toBeVisible();
  await expect(page.getByRole('heading', { name: 'Serving Client Credits' })).toBeVisible();
  const ledgerFilters = page.locator('.workspace-card', { has: page.getByRole('heading', { name: 'Ledger Filters' }) });
  const tenantFilter = ledgerFilters.getByRole('combobox', { name: 'Tenant', exact: true });
  const tenantIdFilter = ledgerFilters.getByLabel('Tenant ID');
  const userFilter = ledgerFilters.getByRole('combobox', { name: 'User', exact: true });
  const userIdFilter = ledgerFilters.getByLabel('User ID');
  await expect(tenantFilter.locator('option').first()).toHaveText('All');
  await expect(userFilter.locator('option').first()).toHaveText('All');
  await expect(page.locator('.workspace-card', { has: page.getByRole('heading', { name: 'Top Serving Users' }) }).getByText('usr_admin')).toBeVisible();
  await expect(page.locator('.workspace-card', { has: page.getByRole('heading', { name: 'Serving Client Credits' }) }).getByText('usr_admin')).toBeVisible();
  await expect(page.locator('.workspace-card', { has: page.getByRole('heading', { name: 'Top Serving Users' }) }).getByText('unknown')).toHaveCount(0);
  await userIdFilter.fill('usr_other');
  await expect(page.locator('.workspace-card', { has: page.getByRole('heading', { name: 'Serving Client Credits' }) }).getByText('usr_other')).toBeVisible();
  await expect(page.locator('.workspace-card', { has: page.getByRole('heading', { name: 'Serving Client Credits' }) }).getByText('usr_admin')).toHaveCount(0);
  await userFilter.selectOption('');
  await tenantIdFilter.fill('ten_default');
  await expect(page.locator('.workspace-card', { has: page.getByRole('heading', { name: 'Serving Client Credits' }) }).getByText('usr_other')).toHaveCount(0);
  await expect(page.getByRole('link', { name: 'req_ledger_1' })).toBeVisible();
  await expect(page.getByText('Token Amount')).toBeVisible();
  await page.getByRole('link', { name: 'req_ledger_1' }).click();
  await expect(page).toHaveURL(/\/dashboard\/ledger$/);
  await expect(page.getByRole('dialog')).toBeVisible();
  await expect(page.getByRole('heading', { name: 'Request Details' })).toBeVisible();
  await page.getByRole('button', { name: 'Close Dialog' }).click();
  await page.getByRole('button', { name: 'Drain' }).click();
  await expect(page.getByText('Drain complete: 1 processed, 0 retried, 0 terminal failures.')).toBeVisible();
});

test('ledger page renders tenant admin and regular user scopes', async ({ page }) => {
  await openLedger(page, { id: 'usr_tenant_admin', tenantId: 'ten_default', email: 'tenant-admin@xeno.test', isAdmin: false, isTenantAdmin: true, active: true });
  await expect(page.getByRole('button', { name: 'Drain' })).toBeVisible();
  await expect(page.getByRole('heading', { name: 'Ledger Balances' })).toBeVisible();

  await page.unrouteAll({ behavior: 'ignoreErrors' });
  await openLedger(page, { id: 'usr_regular', tenantId: 'ten_default', email: 'regular@xeno.test', isAdmin: false, isTenantAdmin: false, active: true });
  await expect(page.getByRole('button', { name: 'Drain' })).toHaveCount(0);
  await expect(page.getByRole('link', { name: 'NetLedger UI' })).toHaveCount(0);
  await expect(page.getByText('70').first()).toBeVisible();
});

test('ledger page renders empty and NetLedger error states', async ({ page }) => {
  const empty = { items: [], total: 0, page: 1, pageSize: 100 };
  await openLedger(page, { id: 'usr_regular', tenantId: 'ten_default', email: 'regular@xeno.test', isAdmin: false, isTenantAdmin: false, active: true }, { balances: empty, entries: { items: [], total: 0 } });
  await expect(page.getByText('No Data').first()).toBeVisible();

  await page.unrouteAll({ behavior: 'ignoreErrors' });
  await openLedger(page, { id: 'usr_regular', tenantId: 'ten_default', email: 'regular@xeno.test', isAdmin: false, isTenantAdmin: false, active: true }, { statusError: true });
  await expect(page.getByText('NetLedger is unavailable.')).toBeVisible();
});

test('ledger page remains readable on mobile viewport', async ({ page }) => {
  await page.setViewportSize({ width: 390, height: 844 });
  await openLedger(page, { id: 'usr_regular', tenantId: 'ten_default', email: 'regular@xeno.test', isAdmin: false, isTenantAdmin: false, active: true });
  await expect(page.getByRole('heading', { name: 'Ledger', exact: true })).toBeVisible();
  await expect(page.getByRole('heading', { name: 'Balance Trend' })).toBeVisible();
  const overflow = await page.evaluate(() => document.documentElement.scrollWidth - document.documentElement.clientWidth);
  expect(overflow).toBeLessThanOrEqual(4);
});

test('settings page manages ledgering configuration', async ({ page }) => {
  let savedSettings = null;
  await openSettings(page, (body) => {
    savedSettings = body;
  });

  const ledgerPanel = page.locator('.workspace-card', { has: page.getByRole('heading', { name: 'Ledger Settings' }) });
  await expect(ledgerPanel).toBeVisible();
  await expect(ledgerPanel.getByText('The ledger service and NetLedger client are created from these settings during server startup.')).toBeVisible();
  await ledgerPanel.getByLabel('Enabled').check();
  await ledgerPanel.getByLabel('NetLedger Base URL').fill('http://netledger:9200');
  await ledgerPanel.getByLabel('NetLedger Access Key').fill('nl-access');
  await ledgerPanel.getByLabel('NetLedger Secret Key').fill('nl-secret');
  await ledgerPanel.getByLabel('Drain Interval ms').fill('2500');
  await ledgerPanel.getByLabel('Max Batch Size').fill('25');
  await ledgerPanel.getByLabel('Max Queue Depth').fill('500');
  await ledgerPanel.getByLabel('NetLedger Request Timeout ms').fill('15000');
  await ledgerPanel.getByLabel('Create Tenants Automatically').uncheck();
  await ledgerPanel.getByLabel('Create Accounts Automatically').uncheck();
  await ledgerPanel.getByLabel('Commit Entries Immediately').uncheck();

  await page.getByRole('button', { name: 'Save Settings' }).click();
  await expect(page.getByText('Settings saved. Some changes will not take effect until the server is restarted.')).toBeVisible();
  expect(savedSettings?.Ledger).toMatchObject({
    Enabled: true,
    BaseUrl: 'http://netledger:9200',
    ApiKey: 'nl-access',
    SecretKey: 'nl-secret',
    DrainIntervalMs: 2500,
    MaxBatchSize: 25,
    MaxQueueDepth: 500,
    RequestTimeoutMs: 15000,
    CreateTenantsAutomatically: false,
    CreateAccountsAutomatically: false,
    CommitEntriesImmediately: false
  });
});

test('public model leaderboard uses the unauthenticated global API', async ({ page }) => {
  let leaderboardRequest = null;
  let leaderboardRequests = [];
  await page.route(`${serverUrl}/**`, async (route) => {
    const url = new URL(route.request().url());
    if (url.pathname === '/v1.0/api/request-history/model-leaderboard') {
      leaderboardRequest = route.request();
      leaderboardRequests = [...leaderboardRequests, route.request()];
      await fulfillLeaderboard(route);
      return;
    }

    await route.fulfill({ json: [] });
  });

  await page.goto('/leaderboard/models?startUtc=2026-07-01T00%3A00%3A00Z&endUtc=2026-07-08T00%3A00%3A00Z');
  await expect(page.getByRole('heading', { name: 'Model Leaderboard' }).first()).toBeVisible();
  await expect(page.getByText('llama3.2:latest')).toBeVisible();
  await expect(page.getByRole('columnheader', { name: 'Request Count' })).toBeVisible();
  await expect(page.getByRole('row', { name: /llama3\.2:latest/ })).toContainText('2');
  await expect(page.getByRole('combobox', { name: 'Rows Per Page' })).toHaveValue('10');
  await expect(page.getByRole('combobox', { name: 'Rows Per Page' }).locator('option')).toHaveText(['10', '25', '50', '100']);
  await expect.poll(() => leaderboardRequest).not.toBeNull();

  const url = new URL(leaderboardRequest.url());
  expect(url.searchParams.get('fromUtc')).toBe('2026-07-01T00:00:00Z');
  expect(url.searchParams.get('toUtc')).toBe('2026-07-08T00:00:00Z');
  expect(url.searchParams.get('limit')).toBe('10');
  expect(url.searchParams.get('offset')).toBe('0');
  expect(url.searchParams.get('tenantId')).toBeNull();
  expect(leaderboardRequest.headers()).not.toHaveProperty('authorization');
  expect(leaderboardRequest.headers()).not.toHaveProperty('x-tenant-id');

  await page.getByRole('button', { name: 'Last Hour' }).click();
  await expect.poll(() => leaderboardRequests.length).toBeGreaterThan(1);
  const quickRangeUrl = new URL(leaderboardRequests[leaderboardRequests.length - 1].url());
  expect(quickRangeUrl.searchParams.get('fromUtc')).toBeTruthy();
  expect(quickRangeUrl.searchParams.get('toUtc')).toBeTruthy();
  expect(quickRangeUrl.searchParams.get('offset')).toBe('0');
  expect(quickRangeUrl.searchParams.get('tenantId')).toBeNull();
});

test('model leaderboard is global by default for regular users and can be scoped to their tenant', async ({ page }) => {
  const leaderboardRequests = [];
  await page.addInitScript((storedSession) => {
    window.localStorage.setItem('xeno.dashboard.session', JSON.stringify(storedSession));
  }, session('usr_regular'));
  await page.route(`${serverUrl}/**`, async (route) => {
    const url = new URL(route.request().url());
    if (url.pathname === '/api/v1/users') {
      await route.fulfill({ json: [{ id: 'usr_regular', tenantId: 'ten_default', email: 'regular@xeno.test', isAdmin: false }] });
      return;
    }

    if (url.pathname === '/api/v1/tenants') {
      await route.fulfill({ json: [{ id: 'ten_default', name: 'Default' }] });
      return;
    }

    if (url.pathname === '/v1.0/api/request-history/model-leaderboard') {
      leaderboardRequests.push(route.request());
      await fulfillLeaderboard(route);
      return;
    }

    await route.fulfill({ json: [] });
  });

  await page.goto('/dashboard/clients/advertised-models/leaderboard');
  await expect(page.locator('h1', { hasText: 'Model Leaderboard' })).toBeVisible();
  await expect(page.getByText('llama3.2:latest')).toBeVisible();
  await expect(page.getByRole('columnheader', { name: 'Request Count' })).toBeVisible();
  await expect(page.getByRole('row', { name: /llama3\.2:latest/ })).toContainText('2');
  // Regular users get a Global/My-Tenant scope selector, not the admin tenant filter.
  await expect(page.getByRole('combobox', { name: 'Tenant Filter' })).toHaveCount(0);
  await expect(page.getByRole('combobox', { name: 'Scope' })).toBeVisible();
  await expect.poll(() => leaderboardRequests.length).toBeGreaterThan(0);

  // Default scope is global: the initial request carries no tenant id.
  const initialUrl = new URL(leaderboardRequests[0].url());
  expect(initialUrl.searchParams.get('tenantId')).toBeNull();
  expect(initialUrl.searchParams.get('limit')).toBe('10');
  expect(initialUrl.searchParams.get('offset')).toBe('0');
  expect(leaderboardRequests[0].headers()).not.toHaveProperty('authorization');

  // Scoping to "My Tenant" re-requests with the session tenant id.
  await page.getByRole('combobox', { name: 'Scope' }).selectOption('ten_default');
  await expect
    .poll(() => new URL(leaderboardRequests[leaderboardRequests.length - 1].url()).searchParams.get('tenantId'))
    .toBe('ten_default');
});

test('regular tenant members see member dashboard surfaces without admin routes', async ({ page }) => {
  await page.addInitScript((storedSession) => {
    window.localStorage.setItem('xeno.dashboard.session', JSON.stringify(storedSession));
  }, session('usr_regular'));
  await page.route(`${serverUrl}/**`, async (route) => {
    const url = new URL(route.request().url());
    const method = route.request().method();
    if (url.pathname === '/api/v1/users' && method === 'GET') {
      await route.fulfill({ json: [{ id: 'usr_regular', tenantId: 'ten_default', username: 'regular@xeno.test', email: 'regular@xeno.test', isAdmin: false, isTenantAdmin: false, active: true }] });
      return;
    }

    if (url.pathname === '/api/v1/tenants' && method === 'GET') {
      await route.fulfill({ json: [{ id: 'ten_default', name: 'Default', active: true }] });
      return;
    }

    if (url.pathname === '/api/v1/credentials' && method === 'GET') {
      await route.fulfill({ json: [{ id: 'cred_regular', tenantId: 'ten_default', userId: 'usr_regular', accessKey: 'ak_regular', active: true }] });
      return;
    }

    if (url.pathname === '/api/v1/credentials' && method === 'POST') {
      const body = route.request().postDataJSON();
      // Members can only pick themselves in the credential form; the server also fences the user to the caller.
      expect(['', 'usr_regular']).toContain(body.userId || '');
      await route.fulfill({ json: { id: 'cred_new', tenantId: 'ten_default', userId: 'usr_regular', accessKey: 'xeno_new', active: true } });
      return;
    }

    if (url.pathname === '/api/v1/roles' && method === 'GET') {
      await route.fulfill({ json: [{ id: 'role_member', tenantId: 'ten_default', name: 'TenantMember', builtIn: true, active: true }] });
      return;
    }

    if (url.pathname === '/api/v1/permissions' && method === 'GET') {
      await route.fulfill({ json: [{ id: 'perm_clients_read', tenantId: 'ten_default', resourceType: 'Clients', operationType: 'Read', permit: true, builtIn: true, active: true }] });
      return;
    }

    if (url.pathname === '/api/v1/effective-permissions' && method === 'GET') {
      await route.fulfill({ json: [{ resourceType: 'Clients', operationType: 'Read', permit: true, sourceRole: 'TenantMember' }] });
      return;
    }

    if (url.pathname === '/api/v1/clients' && method === 'GET') {
      await route.fulfill({
        json: [
          { id: 'client_regular', name: 'member-client', connected: true, models: [{ name: 'member-model', apiType: 'chat', supportsCompletions: true, supportsEmbeddings: false }] },
          { id: 'client_other', name: 'other-client', connected: true, models: [{ name: 'other-model', apiType: 'embedding', supportsCompletions: false, supportsEmbeddings: true }] }
        ]
      });
      return;
    }

    if (url.pathname.startsWith('/v1.0/api/client-telemetry')) {
      await route.fulfill({ json: url.pathname.endsWith('/status') ? { connectedClientCount: 1 } : [] });
      return;
    }

    await route.fulfill({ json: [] });
  });

  await page.goto('/dashboard/home');
  await expect(page.getByRole('link', { name: 'Tenants' })).toBeVisible();
  await expect(page.getByRole('link', { name: 'Users' })).toBeVisible();
  await expect(page.getByRole('link', { name: 'Credentials' })).toBeVisible();
  await expect(page.getByRole('link', { name: 'Security' })).toBeVisible();
  await expect(page.getByRole('link', { name: 'Clients', exact: true })).toBeVisible();
  await expect(page.getByRole('link', { name: 'Client Telemetry' })).toBeVisible();
  await expect(page.getByRole('link', { name: 'Audit Logs' })).toHaveCount(0);
  await expect(page.getByRole('link', { name: 'Danger Zone' })).toHaveCount(0);
  await expect(page.getByRole('link', { name: 'Settings' })).toHaveCount(0);

  await page.getByRole('link', { name: 'Tenants' }).click();
  await expect(page.locator('h1', { hasText: 'Tenants' })).toBeVisible();
  await expect(page.getByRole('button', { name: 'Create Tenant' })).toHaveCount(0);

  await page.getByRole('link', { name: 'Users' }).click();
  await expect(page.locator('h1', { hasText: 'Users' })).toBeVisible();
  await expect(page.getByText('regular@xeno.test').first()).toBeVisible();
  await expect(page.getByRole('button', { name: 'Create User' })).toHaveCount(0);
  await page.getByText('regular@xeno.test').first().click();
  await expect(page.getByRole('heading', { name: 'Edit User' })).toBeVisible();
  await expect(page.getByRole('checkbox', { name: 'Active' })).toBeDisabled();
  await page.getByRole('button', { name: 'Close', exact: true }).click();

  await page.getByRole('link', { name: 'Credentials' }).click();
  await expect(page.locator('h1', { hasText: 'Credentials' })).toBeVisible();
  await expect(page.getByText('ak_regular')).toBeVisible();
  await page.getByRole('button', { name: 'Create Credential' }).click();
  await page.getByRole('dialog').getByRole('button', { name: 'Save' }).click();
  await expect(page.getByText('cred_new')).toBeVisible();

  await page.getByRole('link', { name: 'Security' }).click();
  await expect(page.locator('h1', { hasText: 'Security' })).toBeVisible();
  await expect(page.getByText('TenantMember').first()).toBeVisible();
  await expect(page.getByText('Clients').first()).toBeVisible();
  await expect(page.getByRole('button', { name: 'Create Role' })).toHaveCount(0);
  await expect(page.getByRole('button', { name: 'Create Permission' })).toHaveCount(0);

  await page.getByRole('link', { name: 'Clients', exact: true }).click();
  await expect(page.getByText('member-client')).toBeVisible();
  await page.goto('/dashboard/clients/advertised-models');
  await expect(page.getByText('member-model')).toBeVisible();
  await expect(page.getByText('other-model')).toBeVisible();
  await page.getByRole('textbox', { name: 'Client ID', exact: true }).fill('client_other');
  await expect(page.getByText('other-model')).toBeVisible();
  await expect(page.getByText('member-model')).toHaveCount(0);
  await page.getByRole('link', { name: 'Client Telemetry' }).click();
  await expect(page.getByRole('button', { name: 'Collect Now' })).toHaveCount(0);
});

test('advertised models model-name filters exclude non-matching model rows', async ({ page }) => {
  await page.addInitScript((storedSession) => {
    window.localStorage.setItem('xeno.dashboard.session', JSON.stringify(storedSession));
  }, session('usr_tenant_admin'));
  await page.route(`${serverUrl}/**`, async (route) => {
    const url = new URL(route.request().url());
    if (url.pathname === '/api/v1/users') {
      await route.fulfill({
        json: [
          { id: 'usr_tenant_admin', tenantId: 'ten_default', email: 'tenant-admin@xeno.test', username: 'tenant-admin@xeno.test', isAdmin: false, isTenantAdmin: true, active: true }
        ]
      });
      return;
    }

    if (url.pathname === '/api/v1/clients') {
      await route.fulfill({
        json: [
          {
            id: 'xcl_gpt_a',
            name: 'gpt-host-a',
            connected: true,
            lastHeartbeatUtc: '2026-07-17T16:00:00Z',
            serviceState: 'Normal',
            models: [
              { name: 'gpt-oss:20b', provider: 'Ollama', source: 'ollama', supportsCompletions: true, supportsEmbeddings: false, lastAdvertisedUtc: '2026-07-17T16:00:00Z' },
              { name: 'llama3.2:latest', provider: 'Ollama', source: 'ollama', supportsCompletions: true, supportsEmbeddings: false, lastAdvertisedUtc: '2026-07-17T16:00:00Z' },
              { name: 'all-minilm:latest', provider: 'Ollama', source: 'ollama', supportsCompletions: false, supportsEmbeddings: true, lastAdvertisedUtc: '2026-07-17T16:00:00Z' }
            ]
          },
          {
            id: 'xcl_gpt_b',
            name: 'gpt-host-b',
            connected: true,
            lastHeartbeatUtc: '2026-07-17T16:00:00Z',
            serviceState: 'Normal',
            models: [
              { name: 'gpt-oss:120b', provider: 'Ollama', source: 'ollama', supportsCompletions: true, supportsEmbeddings: false, lastAdvertisedUtc: '2026-07-17T16:00:00Z' }
            ]
          },
          {
            id: 'xcl_openai',
            name: 'openai-host',
            connected: true,
            lastHeartbeatUtc: '2026-07-17T16:00:00Z',
            serviceState: 'Normal',
            models: [
              { name: 'openai-only-model', provider: 'OpenAICompatible', source: 'local-openai', supportsCompletions: true, supportsEmbeddings: false, lastAdvertisedUtc: '2026-07-17T16:00:00Z' }
            ]
          },
          {
            id: 'xcl_mre39er7_pncsizhl3alh69CTFdK',
            name: 'astra',
            connected: false,
            lastHeartbeatUtc: '2026-07-13T07:19:59.0731306Z',
            serviceState: 'Quarantined',
            models: [
              { name: 'llama3.2:latest', provider: 'Ollama', source: 'local-ollama', supportsCompletions: true, supportsEmbeddings: false, lastAdvertisedUtc: '2026-07-13T07:19:37.5123516Z' }
            ]
          }
        ]
      });
      return;
    }

    await route.fulfill({ json: [] });
  });

  await page.goto('/dashboard/clients/advertised-models');
  await expect(page.getByText('llama3.2:latest')).toBeVisible();
  // Client ID and Connected are off by default to keep the table narrow; show every column to filter on them.
  await expect(page.getByRole('columnheader', { name: 'Client ID', exact: true })).toHaveCount(0);
  await page.locator('.column-picker-trigger').first().click();
  await page.locator('.column-picker-popover').getByRole('button', { name: 'Show all' }).click();
  await page.keyboard.press('Escape');
  await expect(page.getByRole('columnheader', { name: 'Client ID', exact: true })).toHaveCount(1);

  await expect(page.getByRole('row', { name: /xcl_mre39er7_pncsizhl3alh69CTFdK/ })).toHaveCount(0);
  await page.getByRole('checkbox', { name: 'Include inactive clients' }).check();
  await expect(page.getByRole('row', { name: /xcl_mre39er7_pncsizhl3alh69CTFdK/ })).toBeVisible();
  await page.getByRole('checkbox', { name: 'Include inactive clients' }).uncheck();

  await page.getByRole('textbox', { name: 'Search', exact: true }).fill('gpt-oss');
  await expect(page.getByText('gpt-oss:20b')).toBeVisible();
  await expect(page.getByText('gpt-oss:120b')).toBeVisible();
  await expect(page.getByText('llama3.2:latest')).toHaveCount(0);
  await expect(page.getByRole('row', { name: /xcl_mre39er7_pncsizhl3alh69CTFdK/ })).toHaveCount(0);
  await page.getByRole('textbox', { name: 'Search', exact: true }).fill('');

  await page.getByRole('combobox', { name: 'Client', exact: true }).selectOption('xcl_gpt_a');
  await expect(page.getByText('gpt-oss:20b')).toBeVisible();
  await expect(page.getByText('llama3.2:latest')).toBeVisible();
  await expect(page.getByText('gpt-oss:120b')).toHaveCount(0);
  await page.getByRole('combobox', { name: 'Client', exact: true }).selectOption('all');

  await page.getByRole('button', { name: 'gpt-host-b', exact: true }).click();
  await expect(page.getByText('gpt-oss:120b')).toBeVisible();
  await expect(page.getByText('gpt-oss:20b')).toHaveCount(0);
  await expect(page.getByText('llama3.2:latest')).toHaveCount(0);
  await page.getByRole('combobox', { name: 'Client', exact: true }).selectOption('all');

  await page.getByRole('textbox', { name: 'Filter Client', exact: true }).fill('gpt-host-b');
  await expect(page.getByText('gpt-oss:120b')).toBeVisible();
  await expect(page.getByText('gpt-oss:20b')).toHaveCount(0);
  await page.getByRole('textbox', { name: 'Filter Client', exact: true }).fill('');

  await page.getByRole('textbox', { name: 'Filter Client ID', exact: true }).fill('xcl_gpt_a');
  await expect(page.getByText('gpt-oss:20b')).toBeVisible();
  await expect(page.getByText('llama3.2:latest')).toBeVisible();
  await expect(page.getByText('gpt-oss:120b')).toHaveCount(0);
  await page.getByRole('textbox', { name: 'Filter Client ID', exact: true }).fill('');

  await page.getByRole('textbox', { name: 'Filter API Type', exact: true }).fill('OpenAI');
  await expect(page.getByText('openai-only-model')).toBeVisible();
  await expect(page.getByText('gpt-oss:20b')).toHaveCount(0);
  await page.getByRole('textbox', { name: 'Filter API Type', exact: true }).fill('');

  await page.getByRole('textbox', { name: 'Filter Source', exact: true }).fill('local-openai');
  await expect(page.getByText('openai-only-model')).toBeVisible();
  await expect(page.getByText('gpt-oss:120b')).toHaveCount(0);
  await page.getByRole('textbox', { name: 'Filter Source', exact: true }).fill('');

  await page.getByRole('textbox', { name: 'Filter Completions', exact: true }).fill('no');
  await expect(page.getByText('all-minilm:latest')).toBeVisible();
  await expect(page.getByText('gpt-oss:20b')).toHaveCount(0);
  await page.getByRole('textbox', { name: 'Filter Completions', exact: true }).fill('');

  await page.getByRole('textbox', { name: 'Filter Embeddings', exact: true }).fill('yes');
  await expect(page.getByText('all-minilm:latest')).toBeVisible();
  await expect(page.getByText('llama3.2:latest')).toHaveCount(0);
  await page.getByRole('textbox', { name: 'Filter Embeddings', exact: true }).fill('');

  await page.getByRole('checkbox', { name: 'Include inactive clients' }).check();
  await page.getByRole('textbox', { name: 'Filter Connected', exact: true }).fill('no');
  await expect(page.getByRole('row', { name: /xcl_mre39er7_pncsizhl3alh69CTFdK/ })).toBeVisible();
  await expect(page.getByText('gpt-oss:20b')).toHaveCount(0);
  await page.getByRole('textbox', { name: 'Filter Connected', exact: true }).fill('');
  await page.getByRole('checkbox', { name: 'Include inactive clients' }).uncheck();

  await expect(page.getByRole('textbox', { name: 'Model Name', exact: true })).toHaveCount(0);
  await page.getByRole('textbox', { name: 'Filter Model Name', exact: true }).fill('gpt');
  await expect(page.getByText('gpt-oss:20b')).toBeVisible();
  await expect(page.getByText('gpt-oss:120b')).toBeVisible();
  await expect(page.getByText('llama3.2:latest')).toHaveCount(0);
  await expect(page.getByRole('row', { name: /xcl_mre39er7_pncsizhl3alh69CTFdK/ })).toHaveCount(0);
});

test('system admin model leaderboard can filter to a selected tenant', async ({ page }) => {
  let leaderboardRequests = [];
  await page.addInitScript((storedSession) => {
    window.localStorage.setItem('xeno.dashboard.session', JSON.stringify(storedSession));
  }, session('usr_admin'));
  await page.route(`${serverUrl}/**`, async (route) => {
    const url = new URL(route.request().url());
    if (url.pathname === '/api/v1/users') {
      await route.fulfill({ json: [{ id: 'usr_admin', tenantId: 'ten_default', email: 'admin@xeno.test', isAdmin: true }] });
      return;
    }

    if (url.pathname === '/api/v1/tenants') {
      await route.fulfill({
        json: [
          { id: 'ten_default', name: 'Default' },
          { id: 'ten_ops', name: 'Operations' }
        ]
      });
      return;
    }

    if (url.pathname === '/v1.0/api/request-history/model-leaderboard') {
      leaderboardRequests = [...leaderboardRequests, route.request()];
      await fulfillLeaderboard(route);
      return;
    }

    await route.fulfill({ json: [] });
  });

  await page.goto('/dashboard/clients/advertised-models/leaderboard?limit=10&offset=0');
  await expect(page.locator('h1', { hasText: 'Model Leaderboard' })).toBeVisible();
  await expect(page.getByRole('combobox', { name: 'Tenant Filter' }).locator('option').first()).toHaveText('All');
  await page.getByRole('combobox', { name: 'Tenant Filter' }).selectOption('ten_ops');
  await expect.poll(() => leaderboardRequests.some((request) => new URL(request.url()).searchParams.get('tenantId') === 'ten_ops')).toBe(true);
  await page.getByRole('textbox', { name: 'Tenant ID', exact: true }).fill('ten_manual');
  await expect.poll(() => leaderboardRequests.some((request) => new URL(request.url()).searchParams.get('tenantId') === 'ten_manual')).toBe(true);
});

test('tenant row actions expose Nuke Tenant only for global admins and show modal results', async ({ page }) => {
  const nukeCalls = [];
  await page.addInitScript((storedSession) => {
    window.localStorage.setItem('xeno.dashboard.session', JSON.stringify(storedSession));
  }, session('usr_admin'));
  await page.route(`${serverUrl}/**`, async (route) => {
    const url = new URL(route.request().url());
    if (url.pathname === '/api/v1/users') {
      await route.fulfill({ json: [{ id: 'usr_admin', tenantId: 'ten_default', email: 'admin@xeno.test', isAdmin: true }] });
      return;
    }

    if (url.pathname === '/api/v1/tenants') {
      await route.fulfill({
        json: [
          { id: 'ten_default', name: 'Default', active: true },
          { id: 'ten_ops', name: 'Operations', active: true }
        ]
      });
      return;
    }

    await route.fulfill({ json: [] });
  });
  await routeNukeEndpoints(page, (kind, body) => { nukeCalls.push({ kind, body }); });

  await page.goto('/dashboard/tenants');
  await expect(page.locator('h1', { hasText: 'Tenants' })).toBeVisible();
  await openRowActions(page.getByRole('row', { name: /ten_ops/ }));
  await page.getByRole('button', { name: 'Nuke Tenant' }).click();
  const dialog = page.getByRole('dialog');
  await expect(dialog.getByRole('heading', { name: 'Nuke Tenant' })).toBeVisible();
  await expect(dialog.getByLabel('Include Audit Records')).toBeChecked();
  await dialog.getByLabel('Confirm Tenant ID').fill('ten_ops');
  await dialog.getByLabel('Reason').fill('Row menu tenant nuke execution');
  await dialog.getByRole('button', { name: 'Execute' }).click();
  await expect.poll(() => nukeCalls.length).toBe(1);
  expect(nukeCalls[0]).toMatchObject({ kind: 'tenant', body: { tenantId: 'ten_ops', confirmTenantId: 'ten_ops', dryRun: false, includeAuditRecords: true } });
  await expect(dialog.getByText('row_nuke_tenant_result')).toBeVisible();
  await expect(dialog.getByText('Deleted Records')).toBeVisible();
});

test('tenant row actions hide Nuke Tenant for non-global admins', async ({ page }) => {
  await page.addInitScript((storedSession) => {
    window.localStorage.setItem('xeno.dashboard.session', JSON.stringify(storedSession));
  }, session('usr_tenant_admin'));
  await page.route(`${serverUrl}/**`, async (route) => {
    const url = new URL(route.request().url());
    if (url.pathname === '/api/v1/users') {
      await route.fulfill({ json: [{ id: 'usr_tenant_admin', tenantId: 'ten_default', email: 'tenant-admin@xeno.test', isAdmin: false, isTenantAdmin: true }] });
      return;
    }

    if (url.pathname === '/api/v1/tenants') {
      await route.fulfill({ json: [{ id: 'ten_default', name: 'Default', active: true }] });
      return;
    }

    await route.fulfill({ json: [] });
  });

  await page.goto('/dashboard/tenants');
  await expect(page.locator('h1', { hasText: 'Tenants' })).toBeVisible();
  await openRowActions(page.getByRole('row', { name: /ten_default/ }));
  await expect(page.getByRole('button', { name: 'Nuke Tenant' })).toHaveCount(0);
});

test('user row actions expose Nuke User for tenant admins and show modal results', async ({ page }) => {
  const nukeCalls = [];
  const users = [
    { id: 'usr_tenant_admin', tenantId: 'ten_default', username: 'tenant-admin@xeno.test', email: 'tenant-admin@xeno.test', isAdmin: false, isTenantAdmin: true, active: true },
    { id: 'usr_target', tenantId: 'ten_default', username: 'target@xeno.test', email: 'target@xeno.test', isAdmin: false, isTenantAdmin: false, active: true }
  ];
  await page.addInitScript((storedSession) => {
    window.localStorage.setItem('xeno.dashboard.session', JSON.stringify(storedSession));
  }, session('usr_tenant_admin'));
  await page.route(`${serverUrl}/**`, async (route) => {
    const url = new URL(route.request().url());
    if (url.pathname === '/api/v1/users') {
      await route.fulfill({ json: users });
      return;
    }

    await route.fulfill({ json: [] });
  });
  await routeNukeEndpoints(page, (kind, body) => { nukeCalls.push({ kind, body }); });

  await page.goto('/dashboard/users');
  await expect(page.locator('h1', { hasText: 'Users' })).toBeVisible();
  await openRowActions(page.getByRole('row', { name: /target@xeno\.test/ }));
  await page.getByRole('button', { name: 'Nuke User' }).click();
  const dialog = page.getByRole('dialog');
  await expect(dialog.getByRole('heading', { name: 'Nuke User' })).toBeVisible();
  await expect(dialog.getByLabel('Include Audit Records')).toBeChecked();
  await dialog.getByLabel('Confirm User ID').fill('usr_target');
  await dialog.getByLabel('Reason').fill('Row menu user nuke execution');
  await dialog.getByRole('button', { name: 'Execute' }).click();
  await expect.poll(() => nukeCalls.length).toBe(1);
  expect(nukeCalls[0]).toMatchObject({ kind: 'user', body: { tenantId: 'ten_default', userId: 'usr_target', confirmUserId: 'usr_target', dryRun: false, includeAuditRecords: true } });
  await expect(dialog.getByText('row_nuke_user_result')).toBeVisible();
  await expect(dialog.getByText('Deleted Records')).toBeVisible();
});

test('regular user editor disables admin flags and omits them from saves', async ({ page }) => {
  const users = [
    { id: 'usr_regular', tenantId: 'ten_default', username: 'regular@xeno.test', email: 'regular@xeno.test', isAdmin: false, isTenantAdmin: false, active: true },
    { id: 'usr_target', tenantId: 'ten_default', username: 'target@xeno.test', email: 'target@xeno.test', isAdmin: false, isTenantAdmin: false, active: true }
  ];
  let updatePayload = null;
  await openUsers(page, 'usr_regular', users, (body) => { updatePayload = body; });

  await page.getByText('regular@xeno.test').first().click();
  await expect(page.getByRole('heading', { name: 'Edit User' })).toBeVisible();
  await expect(page.getByRole('checkbox', { name: 'Active' })).toBeDisabled();
  await expect(page.getByRole('checkbox', { name: 'Tenant Admin' })).toBeDisabled();
  await expect(page.getByRole('checkbox', { name: 'Global Admin' })).toBeDisabled();

  await page.getByRole('textbox', { name: 'Email', exact: true }).fill('regular-updated@xeno.test');
  await page.getByRole('button', { name: 'Save' }).click();
  await expect.poll(() => updatePayload).not.toBeNull();
  expect(updatePayload).not.toHaveProperty('active');
  expect(updatePayload).not.toHaveProperty('isAdmin');
  expect(updatePayload).not.toHaveProperty('isTenantAdmin');
});

test('tenant admin editor can set tenant admin but not global admin', async ({ page }) => {
  const users = [
    { id: 'usr_tenant_admin', tenantId: 'ten_default', username: 'tenant-admin@xeno.test', email: 'tenant-admin@xeno.test', isAdmin: false, isTenantAdmin: true, active: true },
    { id: 'usr_target', tenantId: 'ten_default', username: 'target@xeno.test', email: 'target@xeno.test', isAdmin: false, isTenantAdmin: false, active: true }
  ];
  let updatePayload = null;
  await openUsers(page, 'usr_tenant_admin', users, (body) => { updatePayload = body; });

  await page.getByText('target@xeno.test').first().click();
  await expect(page.getByRole('heading', { name: 'Edit User' })).toBeVisible();
  await expect(page.getByRole('checkbox', { name: 'Tenant Admin' })).toBeEnabled();
  await expect(page.getByRole('checkbox', { name: 'Global Admin' })).toBeDisabled();

  await page.getByRole('checkbox', { name: 'Tenant Admin' }).check();
  await page.getByRole('button', { name: 'Save' }).click();
  await expect.poll(() => updatePayload).not.toBeNull();
  expect(updatePayload).toHaveProperty('isTenantAdmin', true);
  expect(updatePayload).not.toHaveProperty('isAdmin');
});

test('role editor only offers grantable permissions', async ({ page }) => {
  const fixtures = {
    roles: [{ id: 'role_reader', tenantId: 'ten_default', name: 'Reader', permissionIds: ['perm_users_read'], active: true }],
    permissions: [
      { id: 'perm_users_read', tenantId: 'ten_default', resourceType: 'Users', operationType: 'Read', permit: true, active: true },
      { id: 'perm_users_admin', tenantId: 'ten_default', resourceType: 'Users', operationType: 'Admin', permit: true, active: true }
    ],
    grantablePermissions: [
      { id: 'perm_users_read', tenantId: 'ten_default', resourceType: 'Users', operationType: 'Read', permit: true, active: true }
    ]
  };
  await openSecurity(page, fixtures);

  await page.getByRole('button', { name: 'Create Role', exact: true }).click();
  await expect(page.getByRole('heading', { name: 'Create Role' })).toBeVisible();
  await expect(page.getByText('Users / Read / Allow')).toBeVisible();
  await expect(page.getByText('Users / Admin / Allow')).toHaveCount(0);
});

test('role assignment form posts selected principal and role', async ({ page }) => {
  const fixtures = {
    roles: [{ id: 'role_inference', tenantId: 'ten_default', name: 'Inference', permissionIds: ['perm_inference_execute'], active: true }],
    permissions: [{ id: 'perm_inference_execute', tenantId: 'ten_default', resourceType: 'Inference', operationType: 'Execute', permit: true, active: true }],
    grantablePermissions: [{ id: 'perm_inference_execute', tenantId: 'ten_default', resourceType: 'Inference', operationType: 'Execute', permit: true, active: true }],
    users: [{ id: 'usr_target', tenantId: 'ten_default', email: 'target@xeno.test', username: 'target@xeno.test', active: true }],
    credentials: [{ id: 'cred_target', tenantId: 'ten_default', accessKey: 'target-key', active: true }]
  };
  let createPayload = null;
  await openSecurity(page, fixtures, (body) => { createPayload = body; });

  await page.getByRole('button', { name: 'Create Role Assignment' }).click();
  const dialog = page.getByRole('dialog');
  await expect(dialog.getByRole('heading', { name: 'Create Role Assignment' })).toBeVisible();
  await dialog.getByLabel('Principal Type').selectOption('Credential');
  await dialog.getByLabel('Principal ID').selectOption('cred_target');
  await dialog.getByRole('button', { name: 'Save' }).click();
  await expect.poll(() => createPayload).not.toBeNull();
  expect(createPayload).toMatchObject({
    roleId: 'role_inference',
    principalType: 'Credential',
    principalId: 'cred_target',
    active: true
  });
});

test('server chat streams an answer, rates it, and tenant admins review everyone', async ({ page }) => {
  const now = new Date().toISOString();
  const conversation = { Id: 'chconv_1', Title: 'New chat', Model: 'llama3.2:latest', Surface: 'Server', MessageCount: 0, CreatedUtc: now, UpdatedUtc: now, Messages: [] };
  let sent = null;
  let rated = null;
  const scopes = [];
  await page.addInitScript((storedSession) => {
    window.localStorage.setItem('xeno.dashboard.session', JSON.stringify(storedSession));
  }, session('usr_admin'));
  await page.route(`${serverUrl}/**`, async (route) => {
    const url = new URL(route.request().url());
    const method = route.request().method();
    const path = url.pathname;
    if (path === '/api/v1/users' && method === 'GET') {
      await route.fulfill({ json: [{ id: 'usr_admin', tenantId: 'ten_default', email: 'admin@xeno.test', isAdmin: false, isTenantAdmin: true, active: true }] });
      return;
    }
    if (path === '/v1.0/api/chat/models') {
      await route.fulfill({ json: { Entries: [{ Name: 'llama3.2:latest', OnNetwork: true, ServedLocally: false, SupportsCompletions: true }], NetworkMessage: '' } });
      return;
    }
    if (path === '/v1.0/api/chat/models/prepare') {
      await route.fulfill({ json: { Model: 'llama3.2:latest', Route: 'network', Loaded: false, SupportsTools: false, Message: 'Loads when first asked.' } });
      return;
    }
    if (path === '/v1.0/api/chat/conversations' && method === 'GET') {
      await route.fulfill({ json: conversation.Messages.length ? [{ ...conversation, Messages: [] }] : [] });
      return;
    }
    if (path === '/v1.0/api/chat/conversations' && method === 'POST') {
      await route.fulfill({ status: 201, json: { ...conversation, Messages: [] } });
      return;
    }
    if (path === '/v1.0/api/chat/conversations/chconv_1' && method === 'GET') {
      await route.fulfill({ json: conversation });
      return;
    }
    if (path === '/v1.0/api/chat/conversations/chconv_1/messages') {
      sent = route.request().postDataJSON();
      expect(route.request().headers().authorization).toBe('Bearer token-123');
      conversation.Title = 'Hello';
      conversation.Messages = [
        { Id: 'chmsg_u', ConversationId: 'chconv_1', Role: 'User', Content: sent.content },
        { Id: 'chmsg_a', ConversationId: 'chconv_1', Role: 'Assistant', Content: 'Network says hi', Model: 'llama3.2:latest', Route: 'network', TotalMs: 800 }
      ];
      const events = [
        { Kind: 'Turn', TurnId: 'chturn_1' },
        { Kind: 'Chunk', TurnId: 'chturn_1', Text: 'Network says hi' },
        { Kind: 'Done', TurnId: 'chturn_1', Turn: { Id: 'chturn_1', Status: 'Success' } }
      ];
      await route.fulfill({ status: 200, contentType: 'text/event-stream', body: events.map((item) => `data: ${JSON.stringify(item)}\n\n`).join('') });
      return;
    }
    if (path === '/v1.0/api/chat/messages/chmsg_a/feedback') {
      rated = route.request().postDataJSON();
      conversation.Messages[1].Feedback = { Id: 'chfb_1', Rating: rated.rating, Comment: rated.comment };
      await route.fulfill({ json: conversation.Messages[1].Feedback });
      return;
    }
    if (path === '/v1.0/api/chat/analytics') {
      scopes.push(url.searchParams.get('scope') || 'mine');
      await route.fulfill({ json: { Turns: 1, Network: 1, ThumbsDown: 1, NegativeRate: 1, P50Ms: 800, P95Ms: 800, ErrorRate: 0, Buckets: [{ StartUtc: now, Local: 0, Network: 1, Errors: 0 }] } });
      return;
    }
    if (path === '/v1.0/api/chat/history') {
      await route.fulfill({ json: { Items: [{ Id: 'chturn_1', ConversationTitle: 'Hello', Model: 'llama3.2:latest', Route: 'network', Provider: 'gpu-box', Status: 'Success', TotalMs: 800, CompletionTokens: 4, FeedbackRating: -1, CreatedUtc: now }], Total: 1 } });
      return;
    }
    if (path === '/v1.0/api/chat/feedback') {
      await route.fulfill({ json: { Items: [{ Id: 'chfb_1', Rating: -1, Comment: 'Too vague', Model: 'llama3.2:latest', ConversationTitle: 'Hello', Excerpt: 'Network says hi', UpdatedUtc: now }], Total: 1 } });
      return;
    }
    await route.fulfill({ json: [] });
  });

  await page.goto('/dashboard/chat');
  // The chat reaches the bottom of the window without scrolling the page.
  await expect(page.locator('.chat-layout')).toBeVisible();
  const viewport = page.viewportSize();
  const layout = await page.locator('.chat-layout').boundingBox();
  expect(viewport.height - (layout.y + layout.height)).toBeGreaterThanOrEqual(0);
  expect(viewport.height - (layout.y + layout.height)).toBeLessThanOrEqual(24);
  expect(await page.evaluate(() => document.documentElement.scrollHeight - window.innerHeight)).toBeLessThanOrEqual(1);
  await expect(page.locator('h1', { hasText: 'Chat' })).toBeVisible();
  await expect(page.getByRole('combobox', { name: 'Model' }).locator('optgroup[label="Xeno network"] option')).toHaveText(['llama3.2:latest']);
  await expect(page.getByRole('button', { name: /^Tools (on|off)$/ })).toHaveCount(0);
  await expect(page.getByRole('button', { name: 'Streaming on' })).toBeVisible();
  await page.getByRole('textbox', { name: 'Message' }).fill('Hello');
  await page.getByRole('textbox', { name: 'Message' }).press('Enter');
  await expect(page.locator('.chat-message.assistant')).toContainText('Network says hi');
  expect(sent).toEqual({ content: 'Hello', model: 'llama3.2:latest', stream: true, surface: 'Server' });
  await expect(page.locator('.chat-model-status')).toHaveText('Ready');
  await expect(page.locator('.chat-meta')).toContainText('Xeno network');
  await page.getByTitle('Bad answer').click();
  await page.getByRole('dialog').locator('textarea[name="comment"]').fill('Too vague');
  await page.getByRole('dialog').getByRole('button', { name: 'Save rating' }).click();
  await expect.poll(() => rated).toEqual({ rating: -1, comment: 'Too vague' });

  await page.goto('/dashboard/chat-review');
  await expect(page.locator('.metric-card', { hasText: 'Thumbs down share' })).toContainText('100%');
  await expect(page.getByText('Too vague')).toBeVisible();
  await expect(page.getByText('gpu-box')).toBeVisible();
  await page.getByRole('button', { name: 'Everyone in the tenant' }).click();
  await expect.poll(() => scopes.includes('tenant')).toBe(true);
});

async function routeUsage(page, userRecord, onUsage) {
  await page.addInitScript((storedSession) => {
    window.localStorage.setItem('xeno.dashboard.session', JSON.stringify(storedSession));
  }, session(userRecord.id));
  await page.route(`${serverUrl}/**`, async (route) => {
    const url = new URL(route.request().url());
    const path = url.pathname;
    if (path === '/api/v1/users') {
      await route.fulfill({ json: [userRecord] });
      return;
    }
    if (path === '/v1.0/api/request-history/analytics/usage') {
      const result = onUsage ? onUsage(url) : null;
      if (result) await route.fulfill(result);
      else await route.fulfill({ json: usageAnalytics('NetworkRequests') });
      return;
    }
    if (path === '/v1.0/api/chat/analytics/usage') {
      if (onUsage) onUsage(url);
      await route.fulfill({ json: usageAnalytics('Chat') });
      return;
    }
    if (path === '/v1.0/api/client-telemetry/providers/health') {
      await route.fulfill({ json: healthStrip() });
      return;
    }
    if (path === '/v1.0/api/client-telemetry/connected-history') {
      const start = new Date(Date.now() - 900000).toISOString();
      await route.fulfill({ json: { bucketMinutes: 15, buckets: [{ startUtc: start, endUtc: new Date().toISOString(), connected: 2, total: 3 }] } });
      return;
    }
    if (path === '/v1.0/api/request-history') {
      await route.fulfill({ json: { items: [{ id: 'req_1', method: 'POST', path: '/v1/chat/completions', model: 'llama3', statusCode: 200, durationMs: 1000, createdUtc: url.searchParams.get('fromUtc') || new Date().toISOString(), telemetry: { clientRoundTripMs: 900, upstreamTotalMs: 800, requestToHeadersMs: 100, timeToFirstTokenMs: 300, totalStreamingTimeMs: 500 } }], total: 1, page: 1, pageSize: 100 } });
      return;
    }
    if (path === '/v1.0/api/request-history/summary') {
      await route.fulfill({ json: { buckets: [] } });
      return;
    }
    if (path === '/v1.0/api/request-history/analytics/overview' || path === '/v1.0/api/analytics/overview') {
      await route.fulfill({ json: { total: 0, success: 0, failures: 0, failureRate: 0, byModel: [], byPath: [] } });
      return;
    }
    await route.fulfill({ json: [] });
  });
}

test('server usage page follows roles, redirects analytics, and drills into request history', async ({ page }) => {
  const queries = [];
  await routeUsage(page, { id: 'usr_admin', tenantId: 'ten_default', email: 'admin@xeno.test', isAdmin: false, isTenantAdmin: true, active: true }, (url) => { queries.push(url); return null; });
  await page.goto('/dashboard/analytics');
  await expect(page).toHaveURL(/\/dashboard\/usage$/);
  await expect(usageChartOptions(page)).toHaveCount(12);
  const links = await page.locator('.sidebar a').allTextContents();
  expect(links.findIndex((item) => item.includes('Usage'))).toBe(links.findIndex((item) => item.includes('Home')) + 1);
  expect(links.some((item) => item.trim() === 'Analytics')).toBe(false);
  const groupBy = page.locator('.usage-filters label:not(.usage-chart-picker) select').first();
  await expect(groupBy.locator('option')).toHaveText(['Model', 'Provider', 'Client', 'User', 'Credential']);
  await groupBy.selectOption('User');
  await expect.poll(() => queries.at(-1)?.searchParams.get('groupBy')).toBe('User');
  expect(queries.at(-1).searchParams.get('utcOffsetMinutes')).not.toBeNull();

  await page.getByRole('button', { name: 'Chat', exact: true }).last().click();
  await expect(usageChartOptions(page)).toHaveCount(14);
  await expect.poll(() => queries.at(-1)?.searchParams.get('scope')).toBe('tenant');
  await page.getByRole('button', { name: 'Network requests', exact: true }).click();
  await expect(usageChartOptions(page)).toHaveCount(12);

  await showChart(page, 'usage.outcome');
  await page.locator('[data-chart="usage.outcome"] .usage-hover').nth(30).click();
  await expect(page).toHaveURL(/\/dashboard\/requests\?fromUtc=/);
  await expect(page.locator('.usage-drill-banner')).toContainText('from the Usage page');
  await page.locator('tbody tr').first().click();
  await expect(page.locator('.request-detail-timing .usage-bar-row')).toHaveCount(5);
  await expect(page.locator('.request-detail-timing')).toContainText('Relay and client');
});

test('server usage hides groupings a member cannot use and explains a refusal', async ({ page }) => {
  let refuse = false;
  await routeUsage(page, { id: 'usr_member', tenantId: 'ten_default', email: 'member@xeno.test', isAdmin: false, isTenantAdmin: false, active: true }, () => (refuse ? { status: 403, json: { Message: 'Forbidden' } } : null));
  await page.goto('/dashboard/usage');
  await expect(usageChartOptions(page)).toHaveCount(12);
  await expect(page.locator('.usage-filters label:not(.usage-chart-picker) select').first().locator('option')).toHaveText(['Model', 'Provider', 'Client']);
  refuse = true;
  await page.locator('.usage-toolbar .icon-button').click();
  await expect(page.locator('.error-state')).toContainText('Your role cannot see usage grouped this way.');
});

test('server usage fits every width and theme, and client telemetry shows provider health', async ({ page }) => {
  await routeUsage(page, { id: 'usr_admin', tenantId: 'ten_default', email: 'admin@xeno.test', isAdmin: true, isTenantAdmin: true, active: true });
  for (const width of [1280, 768, 390]) {
    for (const theme of ['light', 'dark']) {
      await page.setViewportSize({ width, height: 900 });
      await page.goto('/dashboard/usage');
      await expect(usageChartOptions(page)).toHaveCount(12);
      await page.evaluate((value) => document.documentElement.setAttribute('data-theme', value), theme);
      await page.waitForTimeout(150);
      const overflow = await page.evaluate(() => document.documentElement.scrollWidth - window.innerWidth);
      expect(overflow, `horizontal overflow at ${width} ${theme}`).toBeLessThanOrEqual(1);
      await page.screenshot({ fullPage: true, path: `test-results/usage-server-${width}-${theme}.png` });
    }
  }
  await expect(page.locator('.usage-filters label:not(.usage-chart-picker) select').first().locator('option')).toHaveText(['Model', 'Provider', 'Client', 'User', 'Credential', 'Tenant']);
  await showChart(page, 'connected.clients');
  await expect(page.locator('[data-chart="connected.clients"] rect[data-series="connected"]')).toHaveCount(1);
  await page.setViewportSize({ width: 1280, height: 900 });
  await page.goto('/dashboard/client-telemetry');
  await expect(page.locator('[data-chart="usage.health"] .usage-strip-row')).toHaveCount(1);
  await expect(page.locator('[data-chart="usage.health"]')).toContainText('75% up');
});
