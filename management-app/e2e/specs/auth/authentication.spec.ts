import { expect, test } from '@playwright/test';
import { readSeededUsers, resetBackendDataFiles } from '../../helpers/data-files';

test.describe('Authentication', () => {
  test.beforeEach(async () => {
    await resetBackendDataFiles();
  });

  test('SCN-AUTH-001: first login creates initial user', async ({ page }) => {
    await page.goto('/login');
    await page.getByLabel('Username').fill('bootstrap-admin');
    await page.getByLabel('Password').fill('bootstrap-password-123!');
    await page.getByRole('button', { name: 'Sign in' }).click();

    await expect(page).toHaveURL(/\/clients$/);
    await expect(page.getByRole('heading', { name: 'Clients' })).toBeVisible();

    await expect.poll(async () => (await readSeededUsers()).length).toBe(1);
    const users = await readSeededUsers();
    expect(users[0]?.Username).toBe('bootstrap-admin');
  });

  test('SCN-AUTH-002: invalid login shows error when user exists', async ({ page }) => {
    await page.goto('/login');
    await page.getByLabel('Username').fill('existing-admin');
    await page.getByLabel('Password').fill('correct-password-123!');
    await page.getByRole('button', { name: 'Sign in' }).click();

    await expect(page).toHaveURL(/\/clients$/);

    await page.evaluate(() => {
      localStorage.removeItem('token');
    });

    await page.goto('/login');
    await page.getByLabel('Username').fill('existing-admin');
    await page.getByLabel('Password').fill('wrong-password');
    await page.getByRole('button', { name: 'Sign in' }).click();

    await expect(page).toHaveURL(/\/login$/);
    await expect(page.getByText('Invalid username or password')).toBeVisible();
  });
});
