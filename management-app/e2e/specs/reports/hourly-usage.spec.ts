import { expect, test } from '@playwright/test';
import { login } from '../../helpers/auth';
import { resetBackendDataFiles, seedClients, seedSamples } from '../../helpers/data-files';

test.describe('Reports - Hourly', () => {
  test.beforeEach(async () => {
    await resetBackendDataFiles();
    await seedClients(seedSamples.clientsWithMixedStatus);
  });

  test('renders hourly report and per-user section', async ({ page }) => {
    await login(page);

    await page.goto('/reports/hourly');

    await expect(page.getByRole('heading', { name: 'Hourly Usage' })).toBeVisible();
    await expect(page.getByText(/Total data transferred in the last 24 hours/i)).toBeVisible();
    await expect(page.getByRole('heading', { name: 'Per User Usage' })).toBeVisible();
    await expect(page.getByText('E2E Client Alpha')).toBeVisible();
    await expect(page.getByText('No usage data available')).toHaveCount(0);
  });
});
