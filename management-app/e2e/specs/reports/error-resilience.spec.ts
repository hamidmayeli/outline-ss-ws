import { expect, test, type APIRequestContext } from '@playwright/test';
import { login } from '../../helpers/auth';
import { resetBackendDataFiles, seedClients, seedSamples } from '../../helpers/data-files';

const fakeMetricsBaseUrl = process.env.E2E_FAKE_METRICS_BASE_URL ?? 'http://localhost:3001';

const delay = (ms: number) => new Promise((resolve) => {
  setTimeout(resolve, ms);
});

const recoverMetricsServer = async (request: APIRequestContext) => {
  for (let attempt = 0; attempt < 5; attempt += 1) {
    try {
      const response = await request.get(`${fakeMetricsBaseUrl}/recover`);
      if (response.ok()) {
        return true;
      }
    } catch {
      // retry
    }

    await delay(300);
  }

  return false;
};

test.describe('Reports - Error & resilience', () => {
  test.beforeEach(async ({ request }) => {
    await resetBackendDataFiles();
    await seedClients(seedSamples.clientsWithMixedStatus);
    await recoverMetricsServer(request);
  });

  test('SCN-ERR-001: hourly API error shows fallback error banner', async ({ page }) => {
    await login(page);

    await page.addInitScript(() => {
      const originalFetch = window.fetch.bind(window);
      window.fetch = (input: RequestInfo | URL, init?: RequestInit) => {
        const requestUrl = typeof input === 'string'
          ? input
          : input instanceof URL
            ? input.toString()
            : input.url;

        if (requestUrl.includes('/api/v1/reports/hourly')) {
          return Promise.resolve(new Response(
            JSON.stringify({ message: 'Metrics temporarily unavailable' }),
            {
              status: 500,
              headers: { 'Content-Type': 'application/json' },
            },
          ));
        }

        return originalFetch(input as RequestInfo, init);
      };
    });

    await page.goto('/reports/hourly');

    await expect(page.getByText('Metrics temporarily unavailable')).toBeVisible();
  });

  test('SCN-ERR-002: empty hourly response shows empty-state messaging', async ({ page }) => {
    await login(page);

    await page.addInitScript(() => {
      const originalFetch = window.fetch.bind(window);
      window.fetch = (input: RequestInfo | URL, init?: RequestInit) => {
        const requestUrl = typeof input === 'string'
          ? input
          : input instanceof URL
            ? input.toString()
            : input.url;

        if (requestUrl.includes('/api/v1/reports/hourly')) {
          return Promise.resolve(new Response('[]', {
            status: 200,
            headers: { 'Content-Type': 'application/json' },
          }));
        }

        return originalFetch(input as RequestInfo, init);
      };
    });

    await page.goto('/reports/hourly');

    await expect(page.getByText('No usage data available')).toBeVisible();
  });

  test('SCN-ERR-003: metric server down keeps clients page available and reports empty', async ({ page, request }) => {
    await login(page);

    try {
      const crashResponse = await request.get(`${fakeMetricsBaseUrl}/crash?status=500`);
      expect(crashResponse.ok()).toBeTruthy();

      await page.goto('/clients');
      await expect(page.getByRole('heading', { name: 'Clients' })).toBeVisible();
      await expect(page.locator('tr', { hasText: 'E2E Client Alpha' })).toBeVisible();
      await expect(page.getByText('↑ 0 B').first()).toBeVisible();
      await expect(page.getByText('↓ 0 B').first()).toBeVisible();

      await page.goto('/reports/hourly');
      await expect(page.getByText('No usage data available')).toBeVisible();
    } finally {
      await recoverMetricsServer(request);
    }
  });
});
