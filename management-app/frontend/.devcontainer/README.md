# Frontend Dev Container

This dev container provides a complete Node.js development environment for the Outline Config Server frontend.

## Features

- Node.js 22 with TypeScript (using official Microsoft dev container image)
- npm and yarn pre-installed
- Essential VS Code extensions for React/TypeScript/Vite development
- Git and GitHub CLI pre-installed
- All dependencies managed by the official image

## Usage

### Opening in Dev Container

1. **Using VS Code:**
   - Open the `frontend` folder in VS Code
   - Press `F1` or `Ctrl+Shift+P` (or `Cmd+Shift+P` on Mac)
   - Select "Dev Containers: Reopen in Container"
   - Wait for the container to build and initialize

2. **Using VS Code Remote Containers:**
   - Click the green button in the bottom-left corner
   - Select "Reopen in Container"

### Running the Application

Once inside the container:

```bash
# Start the development server
npm run dev

# Build for production
npm run build

# Preview production build
npm run preview

# Run linting
npm run lint

# Run Cypress tests (E2E)
npm run test-e2e

# Run Cypress tests (Component)
npm run test-component
```

### Accessing the Application

- Development server: http://localhost:5173
- Preview server: http://localhost:4173

## Volumes

The dev container mounts:
- `frontend` folder → `/workspaces/frontend` (your source code with cached consistency)

## Environment Variables

The container sets:
- `NODE_ENV=development`
- `CHOKIDAR_USEPOLLING=true` (for file watching in containers)

## Customization

Edit `.devcontainer/devcontainer.json` to:
- Add more VS Code extensions
- Change port mappings
- Add environment variables
- Add post-create commands
- Add dev container features (e.g., Docker-in-Docker, Python, etc.)

## Notes

- File changes are automatically detected and trigger hot reload
- If you need Cypress for E2E testing, it will be installed via `npm install`
- The cached consistency mount provides good performance for file operations
