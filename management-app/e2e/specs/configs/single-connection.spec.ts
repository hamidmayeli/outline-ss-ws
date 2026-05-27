import { expect, test } from '@playwright/test';
import { readOutlineConfig, readRuntimeFile, resetBackendDataFiles, seedClients, seedSamples } from '../../helpers/data-files';

test.describe('Config endpoint - Single Connection', () => {
  test.beforeEach(async () => {
    await resetBackendDataFiles();
    await seedClients(seedSamples.singleConnectionClient);
  });

  test('SCN-CONF-003: single connection client regenerates secret on each config request', async ({ request }) => {
    const response1 = await request.get('/api/v1/config/e2e-single-conn');
    expect(response1.status()).toBe(200);
    const payload1 = await response1.json();

    const response2 = await request.get('/api/v1/config/e2e-single-conn');
    expect(response2.status()).toBe(200);
    const payload2 = await response2.json();

    expect(payload1.transport.tcp.secret).not.toBe(payload2.transport.tcp.secret);
    expect(payload1.transport.udp.secret).not.toBe(payload2.transport.udp.secret);
  });

  test('SCN-CONF-004: single connection client secret is updated in persisted data', async ({ request }) => {
    const originalSecret = 'single-conn-secret';

    const response = await request.get('/api/v1/config/e2e-single-conn');
    expect(response.status()).toBe(200);
    const payload = await response.json();

    expect(payload.transport.tcp.secret).not.toBe(originalSecret);

    const clientsJson = await readRuntimeFile('clients.json');
    const clients = JSON.parse(clientsJson) as Array<{ Id: string; Secret: string }>;
    const updatedClient = clients.find((c) => c.Id === 'e2e-single-conn');

    expect(updatedClient).toBeDefined();
    expect(updatedClient!.Secret).toBe(payload.transport.tcp.secret);
    expect(updatedClient!.Secret).not.toBe(originalSecret);
  });

  test('SCN-CONF-005: single connection client syncs new secret to outline config', async ({ request }) => {
    const originalSecret = 'single-conn-secret';

    const response = await request.get('/api/v1/config/e2e-single-conn');
    expect(response.status()).toBe(200);
    const payload = await response.json();

    const outlineConfig = await readOutlineConfig();
    expect(outlineConfig).toContain(`secret: ${payload.transport.tcp.secret}`);
    expect(outlineConfig).not.toContain(`secret: ${originalSecret}`);
  });

  test('SCN-CONF-006: non-single-connection client keeps same secret across requests', async ({ request }) => {
    await resetBackendDataFiles();
    await seedClients(seedSamples.clientsWithMixedStatus);

    const response1 = await request.get('/api/v1/config/e2e-client-1');
    expect(response1.status()).toBe(200);
    const payload1 = await response1.json();

    const response2 = await request.get('/api/v1/config/e2e-client-1');
    expect(response2.status()).toBe(200);
    const payload2 = await response2.json();

    expect(payload1.transport.tcp.secret).toBe('alpha-secret-key');
    expect(payload2.transport.tcp.secret).toBe('alpha-secret-key');
  });
});
