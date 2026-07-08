import { expect, test } from '@playwright/test';

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
      await route.fulfill({ json: fixtures.users || [] });
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
        'llama3.2:latest': { totalTokens: 130, promptTokens: 50, outputTokens: 80 },
        'mistral:latest': { totalTokens: 60, promptTokens: 25, outputTokens: 35 }
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

  await page.goto('/leaderboard/models?startUtc=2026-07-01T00%3A00%3A00Z&endUtc=2026-07-08T00%3A00%3A00Z&limit=10&offset=0');
  await expect(page.getByRole('heading', { name: 'Model Leaderboard' }).first()).toBeVisible();
  await expect(page.getByText('llama3.2:latest')).toBeVisible();
  await expect.poll(() => leaderboardRequest).not.toBeNull();

  const url = new URL(leaderboardRequest.url());
  expect(url.searchParams.get('fromUtc')).toBe('2026-07-01T00:00:00Z');
  expect(url.searchParams.get('toUtc')).toBe('2026-07-08T00:00:00Z');
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

  await page.goto('/dashboard/clients/advertised-models/leaderboard?limit=10&offset=0');
  await expect(page.locator('h1', { hasText: 'Model Leaderboard' })).toBeVisible();
  await expect(page.getByText('llama3.2:latest')).toBeVisible();
  await expect(page.getByRole('combobox', { name: 'Tenant Filter' })).toHaveCount(0);
  await expect.poll(() => leaderboardRequest).not.toBeNull();

  const url = new URL(leaderboardRequest.url());
  expect(url.searchParams.get('tenantId')).toBe('ten_default');
  expect(leaderboardRequest.headers()).not.toHaveProperty('authorization');
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

test('regular user editor disables admin flags and omits them from saves', async ({ page }) => {
  const users = [
    { id: 'usr_regular', tenantId: 'ten_default', username: 'regular@xeno.test', email: 'regular@xeno.test', isAdmin: false, isTenantAdmin: false, active: true },
    { id: 'usr_target', tenantId: 'ten_default', username: 'target@xeno.test', email: 'target@xeno.test', isAdmin: false, isTenantAdmin: false, active: true }
  ];
  let updatePayload = null;
  await openUsers(page, 'usr_regular', users, (body) => { updatePayload = body; });

  await page.getByText('target@xeno.test').first().click();
  await expect(page.getByRole('heading', { name: 'Edit User' })).toBeVisible();
  await expect(page.getByRole('checkbox', { name: 'Tenant Admin' })).toBeDisabled();
  await expect(page.getByRole('checkbox', { name: 'Global Admin' })).toBeDisabled();

  await page.getByRole('textbox', { name: 'Email', exact: true }).fill('target-updated@xeno.test');
  await page.getByRole('button', { name: 'Save' }).click();
  await expect.poll(() => updatePayload).not.toBeNull();
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
