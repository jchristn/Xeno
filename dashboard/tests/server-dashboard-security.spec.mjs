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
