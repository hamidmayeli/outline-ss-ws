import { expect, test } from '@playwright/test';
import { login } from '../../helpers/auth';
import { readOutlineConfig, readRuntimeFile, resetBackendDataFiles } from '../../helpers/data-files';

test.describe('Clients - CRUD and Outline sync', () => {
  test.beforeEach(async () => {
    await resetBackendDataFiles();
  });

  test('SCN-CLIENT-002: create/edit/delete client and sync outline config file', async ({ page }) => {
    await login(page);

    await page.getByRole('button', { name: '+ Add Client' }).click();
    await page.getByLabel('Client Name').fill('E2E CRUD Client');
    await page.getByLabel('Data Limit').fill('2GB');
    await page.getByRole('button', { name: 'Save' }).click();

    const createdRow = page.locator('tr', { hasText: 'E2E CRUD Client' });
    await expect(createdRow).toBeVisible();

    const clientsAfterCreate = JSON.parse(await readRuntimeFile('clients.json')) as Array<{
      Id: string;
      Name: string;
      Secret: string;
      AccessKeyId: number;
    }>;

    const createdClient = clientsAfterCreate.find((client) => client.Name === 'E2E CRUD Client');
    expect(createdClient).toBeDefined();

    await expect.poll(async () => readOutlineConfig()).toContain(`id: ${createdClient!.AccessKeyId}`);
    await expect.poll(async () => readOutlineConfig()).toContain(`secret: ${createdClient!.Secret}`);

    await createdRow.locator('button[title="Edit"]').click();
    await page.getByLabel('Client Name').fill('E2E CRUD Client Updated');
    await page.getByRole('button', { name: 'Save' }).click();

    const updatedRow = page.locator('tr', { hasText: 'E2E CRUD Client Updated' });
    await expect(updatedRow).toBeVisible();

    await updatedRow.locator('button[title="Delete"]').click();
    await page.getByRole('button', { name: 'Delete' }).click();

    await expect(page.locator('tr', { hasText: 'E2E CRUD Client Updated' })).toHaveCount(0);

    await expect.poll(async () => readOutlineConfig()).not.toContain(`secret: ${createdClient!.Secret}`);
  });
});
