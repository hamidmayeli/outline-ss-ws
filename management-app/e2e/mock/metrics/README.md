# E2E Fake Metrics Server

This folder contains `rest_api_faker` configuration used by E2E docker runtime.

## Endpoints served
- `GET /health`
- `GET /crash?status=500|404`
- `GET /recover`
- `GET /api/v1/query`
- `GET /api/v1/query_range`

Responses mimic Prometheus API shape used by backend `MetricsService`.

## Notes
- Data is deterministic and loaded from fixtures in `management-app/e2e/fixtures/metrics`.
- Access key `1` is used for seeded metric series.

## Available scenarios
- `hourly-24h` (24 hourly points)
- `daily-30d` (30 daily points, default for daily queries)
- `daily-retention-edge` (30 daily points with edge-case day-1 values)

## Scenario selection
Scenario can be selected using one of these (in precedence order):
1. Query string: `?scenario=<name>`
2. Header: `x-e2e-metrics-scenario: <name>`
3. Environment variable: `E2E_METRICS_SCENARIO=<name>`

If no explicit scenario is provided:
- `query_range` with `step >= 86400` uses `daily-30d`
- otherwise uses `hourly-24h`

## Outage simulation controls
- `/crash` turns on outage mode for `/api/v1/*` endpoints while keeping `/health` always healthy.
- `/recover` turns outage mode off and restores normal responses.
- Optional query: `status` can be `500` (default) or `404`.
