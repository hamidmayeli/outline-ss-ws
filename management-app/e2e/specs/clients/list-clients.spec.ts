import { expect, test } from '@playwright/test';
import { login } from '../../helpers/auth';
import {
  readSeededClients,
  resetBackendDataFiles,
  seedClients,
  seedSamples,
} from '../../helpers/data-files';

test.describe('Clients', () => {
  test.beforeEach(async () => {
    await resetBackendDataFiles();
    await seedClients(seedSamples.clientsWithMixedStatus);
  });

  test('SCN-CLIENT-001: shows client list and usage summary', async ({ page }) => {
    await login(page);
    const seededClients = await readSeededClients();

    await expect(page.getByRole('heading', { name: 'Clients' })).toBeVisible();
    await expect(page.getByText('Total usage (last 30 days):')).toBeVisible();

    for (const client of seededClients) {
      await expect(page.locator('tr', { hasText: client.Name })).toBeVisible();
    }

    const alphaRow = page.locator('tr', { hasText: 'E2E Client Alpha' });
    const betaRow = page.locator('tr', { hasText: 'E2E Client Beta' });

    await expect(alphaRow.getByText('Active')).toBeVisible();
    await expect(betaRow.getByText('Inactive')).toBeVisible();
  });
});
