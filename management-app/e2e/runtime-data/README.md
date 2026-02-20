# E2E Runtime Data

This folder is bind-mounted to `/app/data` inside the `management-app` E2E container.

Files commonly used by tests:
- `clients.json`
- `users.json`
- `refresh-tokens.json`

Tests can arrange backend state by writing these files before navigating pages.
