import { expect, test } from '@playwright/test';
import { login } from '../../helpers/auth';
import { resetBackendDataFiles, seedClients, seedSamples } from '../../helpers/data-files';

test.describe('Reports - 30 Day Usage', () => {
  test.beforeEach(async () => {
    await resetBackendDataFiles();
    await seedClients(seedSamples.clientsWithMixedStatus);
  });

  test('renders pie chart usage view based on last 30 days data', async ({ page }) => {
    await login(page);

    await page.goto('/reports/piechart');

    await expect(page.getByRole('heading', { name: 'Client Usage Distribution' })).toBeVisible();
    await expect(page.getByText('Data usage by client (last 30 days)')).toBeVisible();
    await expect(page.getByRole('columnheader', { name: 'Client' })).toBeVisible();
    await expect(page.getByRole('columnheader', { name: 'Usage' })).toBeVisible();
    await expect(page.getByRole('columnheader', { name: 'Share' })).toBeVisible();
    await expect(page.locator('tbody tr', { hasText: 'E2E Client Alpha' })).toBeVisible();
  });
});
