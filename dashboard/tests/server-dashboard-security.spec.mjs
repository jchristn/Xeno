import { expect, test } from '@playwright/test';
import fs from 'node:fs/promises';

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

test('tenant model leaderboard scopes regular users to their session tenant', async ({ page }) => {
  let leaderboardRequest = null;
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
      leaderboardRequest = route.request();
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
  await expect(page.getByRole('combobox', { name: 'Rows Per Page' })).toHaveValue('10');
  await expect(page.getByRole('combobox', { name: 'Rows Per Page' }).locator('option')).toHaveText(['10', '25', '50', '100']);
  await expect(page.getByRole('combobox', { name: 'Tenant Filter' })).toHaveCount(0);
  await expect.poll(() => leaderboardRequest).not.toBeNull();

  const url = new URL(leaderboardRequest.url());
  expect(url.searchParams.get('tenantId')).toBe('ten_default');
  expect(url.searchParams.get('limit')).toBe('10');
  expect(url.searchParams.get('offset')).toBe('0');
  expect(leaderboardRequest.headers()).not.toHaveProperty('authorization');
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
      expect(body.userId || '').toBe('');
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
      await route.fulfill({ json: [{ id: 'client_regular', name: 'member-client', connected: true, models: [] }] });
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
  await page.getByRole('link', { name: 'Client Telemetry' }).click();
  await expect(page.getByRole('button', { name: 'Collect Now' })).toHaveCount(0);
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
  await page.getByRole('combobox', { name: 'Tenant Filter' }).selectOption('ten_ops');
  await expect.poll(() => leaderboardRequests.some((request) => new URL(request.url()).searchParams.get('tenantId') === 'ten_ops')).toBe(true);
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
  await dialog.getByLabel('Confirm Tenant ID').fill('ten_ops');
  await dialog.getByLabel('Reason').fill('Row menu tenant nuke execution');
  await dialog.getByRole('button', { name: 'Execute' }).click();
  await expect.poll(() => nukeCalls.length).toBe(1);
  expect(nukeCalls[0]).toMatchObject({ kind: 'tenant', body: { tenantId: 'ten_ops', confirmTenantId: 'ten_ops', dryRun: false } });
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
  await dialog.getByLabel('Confirm User ID').fill('usr_target');
  await dialog.getByLabel('Reason').fill('Row menu user nuke execution');
  await dialog.getByRole('button', { name: 'Execute' }).click();
  await expect.poll(() => nukeCalls.length).toBe(1);
  expect(nukeCalls[0]).toMatchObject({ kind: 'user', body: { tenantId: 'ten_default', userId: 'usr_target', confirmUserId: 'usr_target', dryRun: false } });
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
