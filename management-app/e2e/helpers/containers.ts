import { execFile } from 'node:child_process';
import { promisify } from 'node:util';

const execFileAsync = promisify(execFile);

const FAKE_METRICS_CONTAINER = process.env.E2E_FAKE_METRICS_CONTAINER ?? 'outline-e2e-fake-metrics';
const FAKE_METRICS_HEALTH_URL = process.env.E2E_FAKE_METRICS_HEALTH_URL ?? 'http://localhost:3001/health';

const delay = (ms: number) => new Promise((resolve) => {
  setTimeout(resolve, ms);
});

export async function stopFakeMetricsContainer() {
  await execFileAsync('docker', ['stop', FAKE_METRICS_CONTAINER]);
}

export async function startFakeMetricsContainer() {
  await execFileAsync('docker', ['start', FAKE_METRICS_CONTAINER]);
}

export async function waitForFakeMetricsHealthy(timeoutMs = 60_000) {
  const startedAt = Date.now();

  while (Date.now() - startedAt < timeoutMs) {
    try {
      const response = await fetch(FAKE_METRICS_HEALTH_URL);
      if (response.ok) {
        return;
      }
    } catch {
      // wait and retry
    }

    await delay(1_000);
  }

  throw new Error(`fake-metrics did not become healthy within ${timeoutMs}ms`);
}
