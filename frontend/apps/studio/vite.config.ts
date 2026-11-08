/// <reference types="vitest/config" />
import vue from '@vitejs/plugin-vue';
import { defineConfig } from 'vite';

// Dev: the API runs on :5080 (docker compose or dotnet run); /api is proxied so the
// session cookie stays same-origin, as it will be behind nginx in production.
export default defineConfig({
  plugins: [vue()],
  server: {
    port: 5174,
    proxy: { '/api': { target: process.env.API_URL ?? 'http://localhost:5080', changeOrigin: false } },
  },
  test: { environment: 'node', include: ['src/**/*.test.ts'] },
});
