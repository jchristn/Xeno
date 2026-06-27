import { defineConfig } from 'vite';
import react from '@vitejs/plugin-react';

export default defineConfig({
  plugins: [react()],
  build: {
    outDir: 'dist-local',
    emptyOutDir: true,
    rollupOptions: {
      input: 'index-local.html'
    }
  },
  server: {
    port: 9101
  }
});
