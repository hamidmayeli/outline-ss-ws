import process from 'node:process';

const delay = (ms: number) => new Promise((resolve) => {
  setTimeout(resolve, ms);
});

const waitForHealthy = async (name: string, url: string, timeoutMs = 90_000) => {
  const startedAt = Date.now();

  while (Date.now() - startedAt < timeoutMs) {
    try {
      const response = await fetch(url);
      if (response.ok) {
        return;
      }
    } catch {
      // ignore transient startup errors
    }

    await delay(1_000);
  }

  throw new Error(`${name} did not become healthy within ${timeoutMs}ms (${url})`);
};

async function globalSetup() {
  const appBaseUrl = process.env.E2E_BASE_URL ?? 'http://localhost:8080';
  const fakeMetricsHealthUrl = process.env.E2E_FAKE_METRICS_HEALTH_URL ?? 'http://localhost:3001/health';

  await waitForHealthy('fake-metrics', fakeMetricsHealthUrl);
  await waitForHealthy('management-app', `${appBaseUrl.replace(/\/$/, '')}/health`);
}

export default globalSetup;
