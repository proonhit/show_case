import { defineConfig } from 'vite';

// `pnpm viewer:dev` → demo page for trying models by hand.
export default defineConfig({ root: 'demo', server: { port: 5173 } });
