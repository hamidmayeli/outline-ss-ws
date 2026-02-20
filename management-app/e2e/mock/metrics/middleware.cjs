const fs = require('node:fs');
const path = require('node:path');

const createVectorResponse = (result) => ({
  status: 'success',
  data: {
    resultType: 'vector',
    result,
  },
});

const createMatrixResponse = (result) => ({
  status: 'success',
  data: {
    resultType: 'matrix',
    result,
  },
});

const parseNumber = (value, fallback) => {
  const numeric = Number(value);
  return Number.isFinite(numeric) ? numeric : fallback;
};

const fixturesDirectory = path.resolve(__dirname, '../../fixtures/metrics');

const readFixture = (fileName) => {
  const filePath = path.join(fixturesDirectory, fileName);
  const content = fs.readFileSync(filePath, 'utf8');
  return JSON.parse(content);
};

const fixtures = {
  'hourly-24h': readFixture('hourly-24h.json'),
  'daily-30d': readFixture('daily-30d.json'),
  'daily-retention-edge': readFixture('daily-retention-edge.json'),
};

let crashMode = false;
let crashStatus = 500;

const buildTimestamps = (start, end, step) => {
  const output = [];
  for (let ts = start; ts <= end; ts += step) {
    output.push(ts);
  }

  if (output.length === 0) {
    output.push(start);
  }

  return output;
};

const getScenario = (req, step) => {
  const fromQuery = req.query.scenario;
  const fromHeader = req.headers['x-e2e-metrics-scenario'];
  const fromEnv = process.env.E2E_METRICS_SCENARIO;
  const requested = String(fromQuery ?? fromHeader ?? fromEnv ?? '').trim();

  if (requested && fixtures[requested]) {
    return requested;
  }

  return step >= 86400 ? 'daily-30d' : 'hourly-24h';
};

const repeatValue = (values, index) => {
  if (!Array.isArray(values) || values.length === 0) {
    return 0;
  }

  const boundedIndex = Math.max(0, index % values.length);
  return values[boundedIndex];
};

const buildSeriesValues = (timestamps, values) =>
  timestamps.map((ts, index) => [ts, String(repeatValue(values, index))]);

const sumValues = (values) =>
  (Array.isArray(values) ? values : []).reduce((sum, current) => sum + Number(current || 0), 0);

module.exports = function (req, res, next) {
  if (req.path === '/health') {
    return res.status(200).json({ status: 'ok' });
  }

  if (req.path === '/crash') {
    const requestedStatus = Number(req.query.status);
    if (requestedStatus === 404 || requestedStatus === 500) {
      crashStatus = requestedStatus;
    } else {
      crashStatus = 500;
    }

    crashMode = true;
    return res.status(200).json({ status: 'ok', crashMode: true, crashStatus });
  }

  if (req.path === '/recover') {
    crashMode = false;
    return res.status(200).json({ status: 'ok', crashMode: false });
  }

  if (crashMode && req.path.startsWith('/api/v1/')) {
    return res.status(crashStatus).json({
      status: 'error',
      error: 'simulated metrics outage',
      code: crashStatus,
    });
  }

  if (req.path === '/api/v1/query') {
    const query = String(req.query.query ?? '');
    const scenario = getScenario(req, 86400);
    const fixture = fixtures[scenario] ?? fixtures['daily-30d'];
    const now = Math.floor(Date.now() / 1000);

    if (query.includes('shadowsocks_data_bytes')) {
      const totalUploaded = sumValues(fixture.bytesUploaded);
      const totalDownloaded = sumValues(fixture.bytesDownloaded);

      return res.status(200).json(
        createVectorResponse([
          {
            metric: { access_key: fixture.accessKey, dir: '>' },
            value: [now, String(totalUploaded)],
          },
          {
            metric: { access_key: fixture.accessKey, dir: '<' },
            value: [now, String(totalDownloaded)],
          },
        ]),
      );
    }

    if (query.includes('shadowsocks_tcp_connections_closed')) {
      const totalConnections = sumValues(fixture.connections);

      return res.status(200).json(
        createVectorResponse([
          {
            metric: { access_key: fixture.accessKey },
            value: [now, String(totalConnections)],
          },
        ]),
      );
    }

    if (query.includes('shadowsocks_tunnel_time_seconds')) {
      return res.status(200).json(
        createVectorResponse([
          {
            metric: { access_key: fixture.accessKey },
            value: [now, String(fixture.tunnelTimeSeconds)],
          },
        ]),
      );
    }

    return res.status(200).json(createVectorResponse([]));
  }

  if (req.path === '/api/v1/query_range') {
    const query = String(req.query.query ?? '');
    const start = parseNumber(req.query.start, Math.floor(Date.now() / 1000) - 3600 * 23);
    const end = parseNumber(req.query.end, Math.floor(Date.now() / 1000));
    const step = Math.max(1, parseNumber(req.query.step, 3600));
    const timestamps = buildTimestamps(start, end, step);
    const scenario = getScenario(req, step);
    const fixture = fixtures[scenario] ?? fixtures['daily-30d'];

    if (query.includes('shadowsocks_data_bytes')) {
      return res.status(200).json(
        createMatrixResponse([
          {
            metric: { access_key: fixture.accessKey, dir: '>' },
            values: buildSeriesValues(timestamps, fixture.bytesUploaded),
          },
          {
            metric: { access_key: fixture.accessKey, dir: '<' },
            values: buildSeriesValues(timestamps, fixture.bytesDownloaded),
          },
        ]),
      );
    }

    if (query.includes('shadowsocks_tcp_connections_closed')) {
      return res.status(200).json(
        createMatrixResponse([
          {
            metric: { access_key: fixture.accessKey },
            values: buildSeriesValues(timestamps, fixture.connections),
          },
        ]),
      );
    }

    return res.status(200).json(createMatrixResponse([]));
  }

  next();
};
