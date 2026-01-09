# Target
The end goal is to have
- A docker compose file which runs
    1. outline-ss-server
    2. Management app (created in this repo)
    3. A reverse proxy (nginx) sending the websocket traffic to outline and normal traffic to management app
- A script which setup a server running this docker compose, handling SSL Cert and etc.
- A Management app

# Outline server
The outline server is a docker file created in this repo. It should install latest version of [Outline SS Server](https://github.com/OutlineFoundation/tunnel-server/) and have it running. 


# Management app

## Backend
A [minimal API with C# (Native AOT)](../management-api/backend/OutlineManager/OutlineManager.slnx) having the following features:
- Data will be stored in JSON
- An authentication with JWT
- An endpoint exposing the [Outline client config](../supporting-doc/clientConfig.md) (allows anonymous requests GET `/api/v1/config/{UserId}`).
- Endpoints to create/edit/delete a users.
- When a user created/deleted api updates the outline ss server config and trigger a reload for it.

## Frontend
A [React, Typescript, WPA](../management-api/frontend/outline-manager/) with the following features.
- Login
- List of users
- CRUD on users

# Implementation
For outline server and reverse proxy you can get the idea of how to do it in [ss-over-ws.sh](../supporting-doc/ss-over-ws.sh).

For the management app you can look into [outline-config-server](https://github.com/hamidmayeli/outline-config-server). The management app both client and server should be:
- SOLID
- The code should be human readable and follow the best practices.

## CI/CD
- Any change in [management-api](/management-api/) should build its [dockerfile](/management-api/Dockerfile) and push it the DockerHub.
- Outline SS docker build should be triggered manually.
