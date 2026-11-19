import { defineConfig } from 'vite'
import react from '@vitejs/plugin-react'

// Em dev, o backend .NET roda em http://localhost:5000 e espera as rotas com o prefixo /api.
export default defineConfig({
  plugins: [react()],
  server: {
    port: 3000,
    proxy: {
      '/api': {
        target: 'http://localhost:5000',
        changeOrigin: true
      }
    }
  }
})
