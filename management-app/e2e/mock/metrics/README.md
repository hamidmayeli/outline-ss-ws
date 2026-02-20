# E2E Fake Metrics Server

This folder contains `rest_api_faker` configuration used by E2E docker runtime.

## Endpoints served
- `GET /health`
- `GET /api/v1/query`
- `GET /api/v1/query_range`

Responses mimic Prometheus API shape used by backend `MetricsService`.

## Notes
- Data is deterministic and generated from requested `start/end/step`.
- Access key `1` is used for seeded metric series.
- This is phase-2 runtime wiring; scenario-specific fixtures can be added in phase 3.
