import { expect, test } from '@playwright/test';
import { resetBackendDataFiles, seedClients, seedSamples } from '../../helpers/data-files';

test.describe('Config endpoint', () => {
  test.beforeEach(async () => {
    await resetBackendDataFiles();
    await seedClients(seedSamples.clientsWithMixedStatus);
  });

  test('SCN-CONF-001: config endpoint is public and returns valid payload', async ({ request }) => {
    const response = await request.get('/api/v1/config/e2e-client-1');

    expect(response.status()).toBe(200);
    const payload = await response.json();

    expect(payload.transport).toBeDefined();
    expect(payload.transport['$type']).toBe('tcpudp');
    expect(payload.transport.tcp['$type']).toBe('shadowsocks');
    expect(payload.transport.udp['$type']).toBe('shadowsocks');
    expect(payload.transport.tcp.endpoint.url).toContain('/tcp-ws');
    expect(payload.transport.udp.endpoint.url).toContain('/udp-ws');
  });

  test('SCN-CONF-002: inactive client config request is rejected', async ({ request }) => {
    const response = await request.get('/api/v1/config/e2e-client-2');

    expect(response.status()).toBe(409);
    await expect(response.text()).resolves.toContain('Limits reached');
  });
});
