import { defineConfig, devices } from '@playwright/test';
import process from 'node:process';

const baseURL = process.env.E2E_BASE_URL ?? 'http://localhost:8080';
const isCI = process.env.CI === 'true';

export default defineConfig({
  testDir: './specs',
  globalSetup: './helpers/global-setup.ts',
  timeout: 60_000,
  expect: {
    timeout: 10_000,
  },
  fullyParallel: true,
  forbidOnly: isCI,
  retries: isCI ? 2 : 0,
  workers: isCI ? 2 : undefined,
  reporter: [['html', { open: 'never' }], ['list']],
  use: {
    baseURL,
    trace: 'retain-on-failure',
    screenshot: 'only-on-failure',
    video: 'retain-on-failure',
    actionTimeout: 10_000,
    navigationTimeout: 20_000,
  },
  projects: [
    {
      name: 'chromium',
      use: { ...devices['Desktop Chrome'] },
    },
  ],
});
