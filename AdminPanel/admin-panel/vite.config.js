import { defineConfig } from 'vite'
import react from '@vitejs/plugin-react'

export default defineConfig({
  plugins: [react()],
    server: {
      watch: {
        usePolling: true,
      },
      proxy: {
        '/api': {
          target: 'http://manager:8080',
          changeOrigin: true,
          secure: false,
        },
      }
  },
})
