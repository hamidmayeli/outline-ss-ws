# Backend Dev Container

This dev container provides a complete C# .NET 9 development environment for the Outline Config Server backend.

## Features

- .NET 9 SDK (using official Microsoft dev container image)
- Pre-configured with development HTTPS certificates
- Essential VS Code extensions for C# development
- Git and GitHub CLI pre-installed
- All dependencies managed by the official image

## Usage

### Opening in Dev Container

1. **Using VS Code:**
   - Open the `backend` folder in VS Code
   - Press `F1` or `Ctrl+Shift+P` (or `Cmd+Shift+P` on Mac)
   - Select "Dev Containers: Reopen in Container"
   - Wait for the container to build and initialize

2. **Using VS Code Remote Containers:**
   - Click the green button in the bottom-left corner
   - Select "Reopen in Container"

### Running the Application

Once inside the container:

```bash
# Navigate to the API project
cd API

# Run the application
dotnet run

# Or use watch mode for hot reload
dotnet watch run

# Run tests
cd ../
dotnet test
```

### Accessing the Application

- HTTP: http://localhost:5000
- HTTPS: https://localhost:5001
- Swagger UI: https://localhost:5001/swagger

## Volumes

The dev container mounts:
- `backend` folder → `/workspaces/backend` (your source code with cached consistency)

## Customization

Edit `.devcontainer/devcontainer.json` to:
- Add more VS Code extensions
- Change port mappings
- Add environment variables
- Add post-create commands
- Add dev container features (e.g., Docker-in-Docker, Azure CLI, etc.)
