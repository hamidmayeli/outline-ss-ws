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
A [minimal API with C# (Native AOT)](../management-app/backend/OutlineManager/OutlineManager.slnx) having the following features:
- Data will be stored in JSON
- An authentication with JWT
- An endpoint exposing the [Outline client config](../supporting-doc/clientConfig.md) (allows anonymous requests GET `/api/v1/config/{UserId}`).
- Endpoints to create/edit/delete a users.
- When a user created/deleted api updates the outline ss server config and trigger a reload for it.
    > it should have write access to outline's config file.
- Users data should be stored in the app database (JSON) but the outline server will be synced with it.

## Frontend
A [React, Typescript, WPA](../management-app/frontend/outline-manager/) with the following features.
- Home page which is an static html file named  `home.html` (The idea is each deployment be able to provide their landing page like [home.html](/deployables/home.html))
- Login page
- List of clients (page)
- CRUD on clients (page)
- PWA with auto update

    Example of `vite.config.ts`

    ```ts
    import { defineConfig, loadEnv } from 'vite'
    import react from '@vitejs/plugin-react';
    import tsconfigPaths from 'vite-tsconfig-paths';
    import { VitePWA } from 'vite-plugin-pwa'

    // https://vitejs.dev/config/
    export default defineConfig(({ mode }) => {
    const env = loadEnv(mode, process.cwd(), '');
    const apiHost = env.VITE_API_HOST || '/api';
    
    // Escape special regex characters and create pattern for runtime API calls
    const escapedApiHost = apiHost.replace(/[.*+?^${}()|[\]\\]/g, '\\$&');
    const apiPattern = new RegExp(`${escapedApiHost}/`, 'i');

    return {
        build: {
        sourcemap: true,
        },
        plugins: [
        react(),
        tsconfigPaths(),
        VitePWA({ 
            registerType: 'prompt',
            injectRegister: 'auto',
            workbox:{
            globPatterns: ['**/*.{js,css,html,ico,png,svg,json,woff,woff2,ttf}'],
            runtimeCaching: [
                {
                urlPattern: apiPattern,
                handler: 'NetworkOnly',
                },
            ],
            },
            includeAssets: ['favicon.ico', 'apple-touch-icon.png', 'mask-icon.svg', 'logo192.png', 'logo512.png'],
        manifestFilename: "manifest.json",
        manifest: {
            name: 'Outline Manager',
            short_name: 'OL Mngr',
            description: 'To manage Outline servers.',
            theme_color: '#ffffff',
            icons: [
            {
                src: 'logo192.png',
                sizes: '192x192',
                type: 'image/png'
            },
            {
                src: 'logo512.png',
                sizes: '512x512',
                type: 'image/png'
            }
            ]
        }
        }),
        ],
    };
    });
    ```
- Responsive
- Support dark theme

## Installation script
At the root of the repo there should be a script which:
- Get a domain, email address and installation folder as the input parameters
- It holds the different part of the scripts in functions
- Log the progress with colors
- Gets a TSL Cert
- Creates a docker-compose ([docker compose](/deployables/docker-compose.yaml) should leave as a separated fill and it will be downloaded)
- Run the docker compose up -d
- Creates cron jobs too [update the docker images](/deployables/update.sh) and [renew certs](/deployables/renew-cert.sh) (if renewal need a corn job)
- Sets the firewall

# Implementation
For outline server and reverse proxy you can get the idea of how to do it in [ss-over-ws.sh](../supporting-doc/ss-over-ws.sh).

For the management app you can look into [outline-config-server](https://github.com/hamidmayeli/outline-config-server). The management app both client and server should be:
- SOLID
- The code should be human readable and follow the best practices.

## CI/CD
- Any change in [management-app](/management-app/) should build its [dockerfile](/management-app/Dockerfile) and push it the DockerHub.
- Outline SS docker build should be triggered manually.
