import { expect, test } from '@playwright/test';
import { login } from '../../helpers/auth';
import { readRuntimeFile, resetBackendDataFiles } from '../../helpers/data-files';

test.describe('Clients - Single Connection', () => {
  test.beforeEach(async () => {
    await resetBackendDataFiles();
  });

  test('SCN-CLIENT-003: create client with single connection enabled', async ({ page }) => {
    await login(page);

    await page.getByRole('button', { name: '+ Add Client' }).click();
    await page.getByLabel('Client Name').fill('Single Conn E2E');
    await page.getByLabel('Single Connection').check();
    await page.getByRole('button', { name: 'Save' }).click();

    const createdRow = page.locator('tr', { hasText: 'Single Conn E2E' });
    await expect(createdRow).toBeVisible();
    await expect(createdRow.locator('.single-conn-badge')).toBeVisible();

    const clientsJson = await readRuntimeFile('clients.json');
    const clients = JSON.parse(clientsJson) as Array<{ Name: string; IsSingleConnection: boolean }>;
    const createdClient = clients.find((c) => c.Name === 'Single Conn E2E');
    expect(createdClient).toBeDefined();
    expect(createdClient!.IsSingleConnection).toBe(true);
  });

  test('SCN-CLIENT-004: toggle single connection on existing client', async ({ page }) => {
    await login(page);

    // Create a normal client first
    await page.getByRole('button', { name: '+ Add Client' }).click();
    await page.getByLabel('Client Name').fill('Toggle SC Client');
    await page.getByRole('button', { name: 'Save' }).click();

    const createdRow = page.locator('tr', { hasText: 'Toggle SC Client' });
    await expect(createdRow).toBeVisible();
    await expect(createdRow.locator('.single-conn-badge')).not.toBeVisible();

    // Edit and enable single connection
    await createdRow.locator('button[title="Edit"]').click();
    await page.getByLabel('Single Connection').check();
    await page.getByRole('button', { name: 'Save' }).click();

    await expect(createdRow.locator('.single-conn-badge')).toBeVisible();

    const clientsJson = await readRuntimeFile('clients.json');
    const clients = JSON.parse(clientsJson) as Array<{ Name: string; IsSingleConnection: boolean }>;
    const updatedClient = clients.find((c) => c.Name === 'Toggle SC Client');
    expect(updatedClient).toBeDefined();
    expect(updatedClient!.IsSingleConnection).toBe(true);
  });
});
