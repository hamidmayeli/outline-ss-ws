const MS_PER_HOUR = 60 * 60 * 1000;

const clients = [
  {
    id: 1,
    name: 'Alpha',
    secret: 'alpha-secret',
    cipher: 'aes-256-gcm',
    limit: 10737418240,
    isActive: true,
    usageLast30Days: {
      totalBytesTransferred: 7340032000,
      bytesUploaded: 2147483648,
      bytesDownloaded: 5192548352,
      tunnelTimeSeconds: 86400,
      totalConnections: 124,
    },
  },
  {
    id: 2,
    name: 'Bravo',
    secret: 'bravo-secret',
    cipher: 'chacha20-ietf-poly1305',
    limit: 5368709120,
    isActive: true,
    usageLast30Days: {
      totalBytesTransferred: 4294967296,
      bytesUploaded: 1342177280,
      bytesDownloaded: 2952790016,
      tunnelTimeSeconds: 70200,
      totalConnections: 98,
    },
  },
  {
    id: 3,
    name: 'Charlie',
    secret: 'charlie-secret',
    cipher: 'aes-128-gcm',
    limit: null,
    isActive: false,
    usageLast30Days: {
      totalBytesTransferred: 2147483648,
      bytesUploaded: 805306368,
      bytesDownloaded: 1342177280,
      tunnelTimeSeconds: 28800,
      totalConnections: 43,
    },
  },
];

const configs = [
  { id: 1, value: 'ssconf://alpha' },
  { id: 2, value: 'ssconf://bravo' },
  { id: 3, value: 'ssconf://charlie' },
];

const login = { token: 'mock-token' };

const generateHourlyPoints = (startTime, hours, baseBytes, varianceBytes) => {
  const points = [];
  for (let i = 0; i < hours; i += 1) {
    const timestamp = new Date(startTime + i * MS_PER_HOUR);
    const noise = (Math.random() - 0.5) * varianceBytes;
    const bytesTransferred = Math.max(0, Math.round(baseBytes + noise));
    const connections = Math.max(1, Math.round(1 + Math.random() * 7));

    points.push({
      timestamp: timestamp.toISOString(),
      bytesTransferred,
      connections,
    });
  }
  return points;
};

const generateHourlyUsage = (days = 30) => {
  const hours = days * 24;
  const now = new Date();
  now.setMinutes(0, 0, 0);
  const startTime = now.getTime() - hours * MS_PER_HOUR;

  return clients.map((client, index) => {
    const baseBytes = 65_000_000 + index * 35_000_000;
    const varianceBytes = 25_000_000 + index * 10_000_000;

    return {
      id: client.id,
      clientId: String(client.id),
      clientName: client.name,
      dataPoints: generateHourlyPoints(startTime, hours, baseBytes, varianceBytes),
    };
  });
};

module.exports = {
  clients,
  hourly_usage: generateHourlyUsage(),
  configs,
  login,
};
