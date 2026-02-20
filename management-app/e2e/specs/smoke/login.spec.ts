import { expect, test } from '@playwright/test';
import { login } from '../../helpers/auth';
import { resetBackendDataFiles } from '../../helpers/data-files';

test.describe('Smoke', () => {
  test.beforeEach(async () => {
    await resetBackendDataFiles();
  });

  test('unauthenticated user is redirected to login', async ({ page }) => {
    await page.goto('/clients');
    await expect(page).toHaveURL(/\/login$/);
    await expect(page.getByRole('heading', { name: 'Outline Manager' })).toBeVisible();
  });

  test('user can sign in and reach clients page', async ({ page }) => {
    await login(page);
  });
});
