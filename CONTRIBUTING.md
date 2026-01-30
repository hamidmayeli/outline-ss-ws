# Contributing

Thanks for your interest in contributing.

## How to contribute
1) Fork the repo and create a feature branch.
2) Make changes with clear, focused commits.
3) Open a PR against the main branch.

## Best practices
- Keep changes small and well scoped.
- Update documentation when behavior changes.
- Prefer readable, maintainable code over clever shortcuts.
- Avoid unrelated refactors in the same PR.
- Keep APIs backward compatible unless discussed in the PR.

## Quality checks
Before opening a PR, run the following from [management-app/frontend](management-app/frontend):
```sh
pnpm install
pnpm run lint
pnpm run build
```

From [management-app/backend](management-app/backend):
```sh
dotnet test ./OutlineManager.slnx
```

## Docker image expectations
- Any change under [management-app](management-app) must build [management-app/Dockerfile](management-app/Dockerfile) and publish the Docker image.
- The Outline SS Docker build is triggered manually.

## PR checklist
- Tests are passing.
- Docs updated where needed.
- No secrets or environment files committed.
