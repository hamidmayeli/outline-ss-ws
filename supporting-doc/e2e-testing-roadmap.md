# E2E Testing Roadmap (Backend + Frontend)

## Scope
Create end-to-end tests that validate the full flow across:
- `management-app/frontend` (UI + browser behavior)
- `management-app/backend` (API behavior)

Constraint for this E2E suite:
- Real metrics source is **not** used in tests.
- Metrics endpoint is mocked using `rest_api_faker` (NPM package).
- E2E project is kept in a **separate folder** inside `management-app/e2e`.

## End Result (Target State)
At the end of this roadmap, the workspace has:
- A standalone Playwright-based E2E project under `management-app/e2e/`.
- Deterministic mocked metrics responses via `rest_api_faker` for chart/report scenarios.
- Docker-driven execution that tests the combined app image from `management-app/Dockerfile`.
- Repeatable local and CI-friendly commands for running E2E tests.
- A clear scenarios catalog that can be expanded over time.

## Proposed File/Folder Structure

```text
management-app/
  Dockerfile                         # source of tested app image (frontend+backend)
  backend/
    API.Tests/
      E2EContract/
        MetricsContractTests.cs      # optional contract checks for DTO compatibility
  e2e/
    package.json
    playwright.config.ts
    .env.example
    docker/
      docker-compose.e2e.yaml         # app container + fake metric server
    mock/
      metrics/
        db.cjs
        routes.json
        middleware.cjs
        README.md
    fixtures/
      metrics/
        hourly-24h.json
        daily-30d.json
        daily-retention-edge.json
    helpers/
      auth.ts
      containers.ts
      waitFor.ts
    specs/
      smoke/
        login.spec.ts
      reports/
        hourly-usage.spec.ts
        daily-usage-30d.spec.ts
        retention-window.spec.ts
      clients/
        list-clients.spec.ts
    scenarios.placeholder.md
    README.md
```

## Implementation Roadmap

### Phase 1: Standalone E2E Project Bootstrap
1. Create `management-app/e2e/` with independent Node tooling (`@playwright/test`, `rest_api_faker`).
2. Add `management-app/e2e/playwright.config.ts` with:
   - `baseURL` from env (`E2E_BASE_URL`, default `http://localhost:8080`).
   - retries/timeouts suitable for CI.
   - trace/screenshot/video policy on failure.
3. Add scripts in `management-app/e2e/package.json`:
   - `e2e:test`
   - `e2e:test:headed`
   - `e2e:test:ui`
   - `e2e:report`
   - `e2e:up`
   - `e2e:down`

### Phase 2: Container-Orchestrated Test Runtime
1. Build test target image from `management-app/Dockerfile`.
2. Run fake metric server using `rest_api_faker` in dedicated container/service.
3. Run app container from built image and wire it to fake metric server via environment/config.
4. Wait for app health endpoint before Playwright starts.

### Phase 3: Deterministic API/Metric Mocking
1. Add dedicated mock config under `e2e/mock/metrics`.
2. Use `rest_api_faker` for metrics/report endpoints consumed by app backend/frontend.
3. Ensure metrics/report endpoints return fixed datasets for:
   - hourly (24h)
   - daily (30d)
   - retention edge case (e.g., exactly 30 points, oldest disappears only after day 31)
4. Add startup helper to validate fake server readiness before app assertions.

### Phase 4: Core E2E Flows
1. Authentication flow (login success/failure).
2. Client list visibility and basic interaction.
3. Report pages/charts render with mocked hourly and daily data.
4. Validate 30-day expectation behavior from mocked dataset (non-flaky assertion strategy).

### Phase 5: Backend-Frontend Contract Confidence (Optional but recommended)
1. Add lightweight backend contract tests for report DTO shape (`HourlyUsageResponse`, `DailyUsageResponse`).
2. Keep fixture JSON aligned with contract tests to prevent frontend mock drift.

### Phase 6: CI Integration
1. Add CI job steps:
   - install `e2e` deps
   - install Playwright browsers
   - build app image from `management-app/Dockerfile`
   - start fake metric server + app container
   - run Playwright tests from `e2e`
   - stop/remove containers
2. Publish Playwright HTML report/artifacts on failures.

## Expected Execution Flow
1. Build image from `management-app/Dockerfile`.
2. Start fake metrics server (rest_api_faker).
3. Start app container (frontend+backend) configured to call fake metrics server.
4. Run Playwright tests against the app URL.
5. Tear down all E2E containers and collect reports.

## Test Data Strategy
- Use static fixtures for deterministic outcomes.
- Keep one fixture per scenario intent.
- Avoid time-dependent assertions unless clock is controlled in test.
- Prefer asserting chart/table semantics (labels, totals, counts) over pixel snapshots.

## Scenarios Placeholder (To Be Filled)

> This section is intentionally a placeholder catalog. Each item should later be expanded into Given/When/Then + fixture mapping.

### Authentication
- [x] SCN-AUTH-001: When there is not any user, login creates a user.
- [x] SCN-AUTH-002: When there is users, invalid login shows error and stays on login page.
- [x] SCN-AUTH-003: Unauthorized access to protected route redirects to login.

### Clients
- [x] SCN-CLIENT-001: Client list loads and displays expected rows.
- [x] SCN-CLIENT-002: Client create/edit/delete flow updates UI correctly and the the outline config file in backend is updated accordingly.

### Configs
- [x] SCN-CONF-001: Config endpoint is publicly available and returns correct data.
- [x] SCN-CONF-002: Config endpoint takes client limits / activeness into account.

### Reports - Hourly
- [x] SCN-REP-H-001: Hourly chart renders 24 points for selected client/all clients.
- [x] SCN-REP-H-002: Hourly totals match mocked metric values.

### Error/Resilience
- [x] SCN-ERR-001: Metrics API timeout/error shows fallback UI state.
- [x] SCN-ERR-002: Empty metrics response shows empty-state messaging.

## Acceptance Criteria for Roadmap Completion
- Playwright E2E can run locally with one command from `e2e/`.
- Test execution uses a container built from `management-app/Dockerfile`.
- Mocked metrics endpoint is deterministic and documented.
- At least one smoke test and one 30-day report test pass reliably.
- Scenario catalog exists and is ready for incremental implementation.
