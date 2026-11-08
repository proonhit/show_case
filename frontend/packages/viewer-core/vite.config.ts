/// <reference types="vitest/config" />
import { defineConfig } from 'vite';

// Library build: three and vue stay external so apps share one copy.
export default defineConfig({
  build: {
    lib: {
      entry: { index: 'src/index.ts', vue: 'src/vue.ts' },
      formats: ['es'],
    },
    rollupOptions: { external: ['three', /^three\//, 'vue'] },
    sourcemap: true,
  },
  test: { environment: 'node' },
});
