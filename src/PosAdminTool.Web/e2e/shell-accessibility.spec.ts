import { expect, test } from '@playwright/test';
import AxeBuilder from '@axe-core/playwright';

for (const theme of ['light', 'dark'] as const) {
  test(`shell is keyboard usable and has no serious axe findings in ${theme}`, async ({ page }) => {
    await page.goto('/');
    if (theme === 'dark') { await page.getByRole('button', { name: /switch from light theme/i }).click(); }
    await expect(page.getByRole('main')).toContainText('Branch signal');
    await page.locator('.skip-link').focus();
    await expect(page.locator(':focus')).toHaveAttribute('href', '#main-content');
    await page.keyboard.press('Tab');
    await expect(page.locator(':focus')).toHaveAttribute('href', '/');
    const scan = await new AxeBuilder({ page }).disableRules(['color-contrast']).analyze();
    expect(scan.violations.filter((violation) => ['critical', 'serious'].includes(violation.impact ?? '')).map((violation) => violation.id)).toEqual([]);
  });
}

test('signal-path evidence links route to diagnostics without relying on marker colour', async ({ page }) => {
  const externalRequests: string[] = [];
  page.on('request', (request) => { if (!request.url().startsWith('http://127.0.0.1:5001/')) { externalRequests.push(request.url()); } });
  await page.goto('/');
  const server = page.locator('#main-content').getByRole('link', { name: /main server: unreachable/i });
  await expect(server).toContainText('Agent cannot reach');
  await server.click();
  await expect(page).toHaveURL(/downloads/);
  expect(externalRequests).toEqual([]);
});
