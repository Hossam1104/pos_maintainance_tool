import { expect, test } from '@playwright/test';

test('configuration imports, edits, tests, verifies, saves, reloads, and never receives a secret', async ({ page }) => {
  const sqlSentinel = 'sentinel-e2e-sql-password';
  const rdbSentinel = 'sentinel-e2e-rdb-password';
  const requestBodies: unknown[] = [];
  const responseBodies: unknown[] = [];
  let configuration = {
    sqlInstance: 'LOCALHOST', sqlUser: 'operator', hasSqlPassword: false, branchCode: 'B001', posNumber: '03', release: '10.4', clientName: 'DBS', apiBaseUrl: 'https://main.example', databases: ['branch'], services: ['rms'],
    downloader: { apiUrl: 'https://download.example', rdbServerIp: '10.0.0.8', rdbUsername: 'rdb', hasRdbPassword: false, knownBranchCodes: ['B001'], pollIntervalSeconds: 5, timeoutSeconds: 1800 }, version: 1,
  };
  await page.route('**/api/v1/**', async (route) => {
    const request = route.request(); const path = new URL(request.url()).pathname; const method = request.method();
    const reply = async (body: unknown, status = 200) => { responseBodies.push(body); await route.fulfill({ status, contentType: 'application/json', body: JSON.stringify(body) }); };
    if (path.endsWith('/antiforgery')) return reply({ token: 'test-token' });
    if (path.endsWith('/configuration') && method === 'GET') return reply(configuration);
    if (path.endsWith('/device/capabilities')) return reply({ agentVersion: '1.0', operatingSystem: 'Windows', browseRoots: [{ rootId: 'managed-backups', displayName: 'Managed backups' }] });
    if (path.endsWith('/configuration/import-rms')) return reply(configuration);
    if (path.endsWith('/configuration/test-database')) return reply({ evidence: { freshness: 'fresh', lastCheckedUtc: '2026-07-30T00:00:00Z', detail: 'SQL query completed' } });
    if (path.endsWith('/configuration/verify-branch')) return reply({ evidence: { freshness: 'fresh', lastCheckedUtc: '2026-07-30T00:00:00Z', detail: 'Branch exists' } });
    if (path.endsWith('/files/browse')) return reply({ rootId: 'managed-backups', relativeSubPath: '', entries: [{ name: 'daily', isDirectory: true, relativeSubPath: 'daily', sizeBytes: null, lastModifiedUtc: null }] });
    if (path.endsWith('/configuration') && method === 'PUT') {
      const body = request.postDataJSON() as Record<string, any>; requestBodies.push(body);
      configuration = { ...configuration, ...body, hasSqlPassword: Boolean(body.sqlPassword) || configuration.hasSqlPassword, downloader: { ...configuration.downloader, ...body.downloader, hasRdbPassword: Boolean(body.downloader.rdbPassword) || configuration.downloader.hasRdbPassword }, version: configuration.version + 1 };
      delete (configuration as Record<string, unknown>).sqlPassword;
      delete (configuration.downloader as Record<string, unknown>).rdbPassword;
      return reply(configuration);
    }
    return reply({ title: 'Unexpected test request' }, 404);
  });

  await page.goto('/settings');
  await expect(page.getByRole('heading', { name: 'Settings' })).toBeVisible();
  await page.getByRole('button', { name: 'Import RMS+' }).click();
  await page.locator('[formcontrolname="clientName"]').fill('Edited client');
  await page.locator('[formcontrolname="sqlPassword"]').fill(sqlSentinel);
  await page.locator('[formcontrolname="rdbPassword"]').fill(rdbSentinel);
  await page.getByRole('button', { name: 'Test database' }).click();
  await expect(page.getByText('SQL query completed')).toBeVisible();
  await page.getByRole('button', { name: 'Verify branch' }).click();
  await expect(page.getByText('Branch exists')).toBeVisible();
  await page.getByRole('button', { name: 'Browse Managed backups' }).click();
  await expect(page.getByText('daily')).toBeVisible();
  await page.getByRole('button', { name: 'Save settings' }).click();
  await expect(page.getByText('Settings saved.')).toBeVisible();
  await page.reload();

  expect(requestBodies).toContainEqual(expect.objectContaining({ sqlPassword: sqlSentinel, downloader: expect.objectContaining({ rdbPassword: rdbSentinel }) }));
  expect(JSON.stringify(responseBodies)).not.toContain(sqlSentinel);
  expect(JSON.stringify(responseBodies)).not.toContain(rdbSentinel);
  await expect(page.locator('[formcontrolname="sqlPassword"]')).toHaveValue('');
  await expect(page.locator('[formcontrolname="rdbPassword"]')).toHaveValue('');
});
