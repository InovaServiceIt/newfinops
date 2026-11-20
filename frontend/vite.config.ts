import { realpathSync } from 'node:fs'
import { dirname } from 'node:path'
import { fileURLToPath } from 'node:url'
import { defineConfig } from 'vite'
import react from '@vitejs/plugin-react'

// No Windows, a pasta atual pode chegar como "c:\..." enquanto o caminho real é "C:\...".
// Com isso o mesmo módulo às vezes entra duas vezes no bundle (ids "c:/" e "C:/"),
// duplicando contextos React (ex.: ThemeProvider) e deixando a tela em branco.
// Fixar o root no caminho real (com a grafia do sistema de arquivos) evita isso.
const root = realpathSync.native(dirname(fileURLToPath(import.meta.url)))
process.chdir(root)

// Em dev, o backend .NET roda em http://localhost:5000 e espera as rotas com o prefixo /api.
export default defineConfig({
  root,
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
