import { expect, test } from '@playwright/test';
import { login } from '../../helpers/auth';
import { resetBackendDataFiles, seedClients, seedSamples } from '../../helpers/data-files';

const formatBytes = (bytes: number): string => {
  if (bytes === 0) return '0 B';
  const k = 1024;
  const sizes = ['B', 'KB', 'MB', 'GB', 'TB'];
  const i = Math.floor(Math.log(bytes) / Math.log(k));
  return `${(bytes / Math.pow(k, i)).toFixed(2)} ${sizes[i]}`;
};

test.describe('Reports - Hourly', () => {
  test.beforeEach(async () => {
    await resetBackendDataFiles();
    await seedClients(seedSamples.clientsWithMixedStatus);
  });

  test('SCN-REP-H-001: renders hourly report and per-user section', async ({ page }) => {
    await login(page);

    await page.goto('/reports/hourly');

    await expect(page.getByRole('heading', { name: 'Hourly Usage' })).toBeVisible();
    await expect(page.getByText(/Total data transferred in the last 24 hours/i)).toBeVisible();
    await expect(page.getByRole('heading', { name: 'Per User Usage' })).toBeVisible();
    await expect(page.getByText('E2E Client Alpha')).toBeVisible();
    await expect(page.getByText('No usage data available')).toHaveCount(0);
  });

  test('SCN-REP-H-002: hourly totals match mocked metric values', async ({ page }) => {
    await login(page);
    await page.goto('/reports/hourly');

    const uploadedTotal = 2676000;
    const downloadedTotal = 5076000;
    const expectedTotal = uploadedTotal + downloadedTotal;
    const expectedFormatted = formatBytes(expectedTotal);

    await expect(
      page.getByText(`Total data transferred in the last 24 hours (${expectedFormatted})`),
    ).toBeVisible();
  });
});
