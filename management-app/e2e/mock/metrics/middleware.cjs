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

const buildSeriesValues = (timestamps, valueFactory) =>
  timestamps.map((ts, index) => [ts, String(valueFactory(index))]);

module.exports = function (req, res, next) {
  if (req.path === '/health') {
    return res.status(200).json({ status: 'ok' });
  }

  if (req.path === '/api/v1/query') {
    const query = String(req.query.query ?? '');

    if (query.includes('shadowsocks_data_bytes')) {
      return res.status(200).json(
        createVectorResponse([
          {
            metric: { access_key: '1', dir: '>' },
            value: [Math.floor(Date.now() / 1000), '1048576'],
          },
          {
            metric: { access_key: '1', dir: '<' },
            value: [Math.floor(Date.now() / 1000), '2097152'],
          },
        ]),
      );
    }

    if (query.includes('shadowsocks_tcp_connections_closed')) {
      return res.status(200).json(
        createVectorResponse([
          {
            metric: { access_key: '1' },
            value: [Math.floor(Date.now() / 1000), '42'],
          },
        ]),
      );
    }

    if (query.includes('shadowsocks_tunnel_time_seconds')) {
      return res.status(200).json(
        createVectorResponse([
          {
            metric: { access_key: '1' },
            value: [Math.floor(Date.now() / 1000), '3600'],
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

    if (query.includes('shadowsocks_data_bytes')) {
      return res.status(200).json(
        createMatrixResponse([
          {
            metric: { access_key: '1', dir: '>' },
            values: buildSeriesValues(timestamps, (i) => 100000 + i * 1000),
          },
          {
            metric: { access_key: '1', dir: '<' },
            values: buildSeriesValues(timestamps, (i) => 200000 + i * 1000),
          },
        ]),
      );
    }

    if (query.includes('shadowsocks_tcp_connections_closed')) {
      return res.status(200).json(
        createMatrixResponse([
          {
            metric: { access_key: '1' },
            values: buildSeriesValues(timestamps, () => 3),
          },
        ]),
      );
    }

    return res.status(200).json(createMatrixResponse([]));
  }

  next();
};
