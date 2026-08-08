import { expect, test } from '@playwright/test';

test('service commands show a pending state and use the opaque service identifier', async ({ page }) => {
  const initial = [{ serviceId: 'svc-1a2b3c4d', displayName: 'RMS test service', state: 'stopped', lastChecked: { freshness: 'fresh', lastCheckedUtc: '2026-07-30T00:00:00Z', detail: 'Windows service is stopped' }, allowedActions: ['start'], lastOutcome: null }];
  let actionPath = '';

  await page.route('**/api/v1/**', async (route) => {
    const request = route.request(); const path = new URL(request.url()).pathname;
    if (path.endsWith('/antiforgery')) return route.fulfill({ contentType: 'application/json', body: JSON.stringify({ token: 'test-token' }) });
    if (path.endsWith('/services') && request.method() === 'GET') return route.fulfill({ contentType: 'application/json', body: JSON.stringify(initial) });
    if (path.includes('/services/') && path.endsWith('/actions') && request.method() === 'POST') {
      actionPath = path;
      return route.fulfill({ status: 202, contentType: 'application/json', body: JSON.stringify({ ...initial[0], state: 'transitioning', lastChecked: { freshness: 'fresh', lastCheckedUtc: '2026-07-30T00:00:01Z', detail: 'Start command sent; awaiting Agent confirmation' } }) });
    }
    if (path.endsWith('/events')) return route.fulfill({ status: 204 });
    return route.fulfill({ status: 404, contentType: 'application/json', body: '{}' });
  });

  await page.goto('/services');
  await expect(page.getByRole('heading', { name: 'Services', exact: true })).toBeVisible();
  const start = page.getByRole('button', { name: 'Start RMS test service', exact: true });
  await expect(start).toBeEnabled();
  await start.click();

  await expect(page.getByText('Transitioning')).toBeVisible();
  await expect(start).toBeDisabled();
  await expect(page.getByText('Start command sent for RMS test service. Awaiting Agent confirmation.', { exact: true })).toBeVisible();
  expect(actionPath).toBe('/api/v1/services/svc-1a2b3c4d/actions');
});
