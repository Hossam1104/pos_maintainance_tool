import { expect, test } from '@playwright/test';

test('configuration route exposes the write-only settings workflow', async ({ page }) => {
  await page.goto('/settings');
  await expect(page.getByRole('heading', { name: 'Settings' })).toBeVisible();
  await expect(page.getByText(/Passwords are write-only/i)).toBeVisible();
  await expect(page.getByRole('button', { name: 'Save configuration' })).toBeDisabled();
});
