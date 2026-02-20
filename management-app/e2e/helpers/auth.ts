import { expect, type Page } from '@playwright/test';

const DEFAULT_USERNAME = process.env.E2E_USERNAME ?? 'e2e-admin';
const DEFAULT_PASSWORD = process.env.E2E_PASSWORD ?? 'e2e-password-123!';

export async function login(page: Page, username = DEFAULT_USERNAME, password = DEFAULT_PASSWORD) {
  await page.goto('/login');
  await page.getByLabel('Username').fill(username);
  await page.getByLabel('Password').fill(password);
  await page.getByRole('button', { name: 'Sign in' }).click();
  await expect(page).toHaveURL(/\/clients$/);
  await expect(page.getByRole('heading', { name: 'Clients' })).toBeVisible();
}

export async function ensureClientExists(page: Page, clientName = 'E2E Client Alpha') {
  await page.goto('/clients');

  const existingRow = page.locator('tr', { hasText: clientName });
  if (await existingRow.count()) {
    return;
  }

  await page.getByRole('button', { name: '+ Add Client' }).click();
  await page.getByLabel('Client Name').fill(clientName);
  await page.getByLabel('Data Limit').fill('5GB');
  await page.getByRole('button', { name: 'Save' }).click();

  await expect(page.locator('tr', { hasText: clientName })).toBeVisible();
}
