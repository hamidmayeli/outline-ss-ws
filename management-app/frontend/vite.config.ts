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
