import { expect, test } from '@playwright/test';

test('backup workflow reviews a managed destination, recovers after refresh, and exposes an artifact download', async ({ page }) => {
  let submitted = false;
  let detailReads = 0;
  const operation = {
    operationId: 'op-backup-001', operationType: 'backup', state: 'running', progressPercent: 55,
    currentStage: 'compressing', branchCodeSnapshot: 'B001', requestingPrincipal: 'TEST\\admin',
    requestedAtUtc: '2026-08-08T20:00:00Z', startedAtUtc: '2026-08-08T20:00:01Z', endedAtUtc: null,
    ownedResourceLocks: ['sql', 'filesystem-cleanup'], events: [{ atUtc: '2026-08-08T20:00:02Z', stage: 'compressing', message: 'Compressing selected backup items.' }],
    resultArtifactIds: [], errorCode: null, correlationId: 'corr-backup-001', resolvedDestinationReference: 'managed-backups / daily',
  };
  const artifact = { artifactId: 'artifact-backup-001', displayName: 'Client_B001_POS_03_DB_Backup_20260808_200000.zip', sizeBytes: 2048, sha256Checksum: 'abc123', createdAtUtc: '2026-08-08T20:00:03Z' };

  await page.route('**/api/v1/**', async (route) => {
    const request = route.request();
    const path = new URL(request.url()).pathname;
    const method = request.method();
    const reply = (body: unknown, status = 200) => route.fulfill({ status, contentType: 'application/json', body: JSON.stringify(body) });

    if (path.endsWith('/antiforgery')) return reply({ token: 'test-token' });
    if (path.endsWith('/backups/options')) return reply({ branchCode: 'B001', targetDatabase: 'RmsBranchSrv', components: [
      { componentId: 'branch-database', displayName: 'RmsBranchSrv database' },
      { componentId: 'cashier-database', displayName: 'RmsCashierSrv database' },
      { componentId: 'branch-config', displayName: 'Branch appsettings' },
      { componentId: 'cashier-server-config', displayName: 'Cashier server appsettings' },
      { componentId: 'cashier-ui-config', displayName: 'Cashier UI appsettings' },
    ] });
    if (path.endsWith('/device/capabilities')) return reply({ agentVersion: '1.0', operatingSystem: 'Windows', browseRoots: [{ rootId: 'managed-backups', displayName: 'Managed backups' }] });
    if (path.endsWith('/backups') && method === 'GET') return reply(submitted ? [artifact] : []);
    if (path.endsWith('/operations') && method === 'GET') return reply(submitted ? [operation] : []);
    if (path.endsWith('/files/browse')) return reply({ rootId: 'managed-backups', relativeSubPath: '', entries: [{ name: 'daily', isDirectory: true, relativeSubPath: 'daily', sizeBytes: null, lastModifiedUtc: null }] });
    if (path.endsWith('/files/handles')) return reply({ handleId: 'handle-backup-001', purpose: 'backupDestination', expiresAtUtc: '2026-08-08T21:00:00Z' });
    if (path.endsWith('/backups') && method === 'POST') {
      submitted = true;
      return reply({ ...operation, state: 'running' }, 202);
    }
    if (path.endsWith('/operations/op-backup-001') && method === 'GET') {
      detailReads += 1;
      const completed = detailReads >= 4;
      return reply({ ...operation, state: completed ? 'succeeded' : 'running', progressPercent: completed ? 100 : 55, currentStage: completed ? 'succeeded' : 'compressing', endedAtUtc: completed ? '2026-08-08T20:00:03Z' : null, resultArtifactIds: completed ? [artifact.artifactId] : [] });
    }
    if (path.endsWith('/artifacts/artifact-backup-001')) return reply(artifact);
    if (path.endsWith('/events')) return route.fulfill({ status: 204 });
    return reply({ title: 'Unexpected test request' }, 404);
  });

  await page.goto('/backups');
  await expect(page.getByRole('heading', { name: 'Backups', exact: true })).toBeVisible();
  await page.getByRole('button', { name: 'Select all' }).click();
  await page.getByRole('button', { name: 'Managed backups' }).click();
  await page.getByRole('button', { name: 'Use this folder' }).click();
  await page.getByRole('button', { name: 'Review backup' }).click();
  await expect(page.getByText('B001', { exact: true })).toBeVisible();
  await expect(page.getByText('RmsBranchSrv', { exact: true })).toBeVisible();
  await page.getByRole('button', { name: 'Start backup' }).click();
  await expect(page.getByRole('heading', { name: 'Running', exact: true })).toBeVisible();

  await page.reload();
  await expect(page.getByRole('heading', { name: 'Succeeded', exact: true })).toBeVisible();
  await expect(page.getByRole('link', { name: 'Download archive' })).toHaveAttribute('href', '/api/v1/artifacts/artifact-backup-001/content');
  await expect(page.getByText('managed-backups / daily', { exact: true })).toBeVisible();
});
