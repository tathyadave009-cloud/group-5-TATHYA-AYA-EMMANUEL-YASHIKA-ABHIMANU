import react from '@vitejs/plugin-react'
import { defineConfig } from 'vite'

// https://vite.dev/config/
export default defineConfig({
  plugins: [react()],
  server: {
    proxy: {
      '/api': {
        target: 'http://localhost:8080',
        changeOrigin: true,
      },
      // Registration's login service (avoids browser CORS problems)
      '/registration-api': {
        target: 'https://cm-rs-api-gbgrffa8h5e5fdcy.canadaeast-01.azurewebsites.net',
        changeOrigin: true,
        secure: true,
        rewrite: (path) => path.replace(/^\/registration-api/, ''),
      },
    },
  },
})