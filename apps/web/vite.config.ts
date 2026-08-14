import { defineConfig } from 'vitest/config'
import react from '@vitejs/plugin-react'
import tailwindcss from '@tailwindcss/vite'

export default defineConfig({
  plugins: [react(), tailwindcss()],
  test: {
    environment: 'jsdom',
    setupFiles: ['./src/test/setup.ts'],
  },
  server: {
    // Proxy disabled for mock mode. Enable when running backend:
    // proxy: {
    //   '/api': 'http://localhost:5179',
    //   '/hubs': 'http://localhost:5179',
    // },
  },
})
