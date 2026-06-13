import { defineConfig } from 'vite'
import vue from '@vitejs/plugin-vue'
import electron from 'vite-plugin-electron/simple'
import path from 'node:path'

export default defineConfig(({ command }) => {
  const isBuild = command === 'build'
  return {
    plugins: [
      vue(),
      electron({
        main: {
          entry: 'electron/main/index.ts',
          vite: {
            build: {
              outDir: 'dist-electron/main',
              minify: isBuild,
              rollupOptions: {
                external: ['ws', 'bufferutil', 'utf-8-validate'],
              },
            },
          },
        },
        preload: {
          input: 'electron/preload/index.ts',
          vite: {
            build: {
              outDir: 'dist-electron/preload',
              minify: isBuild,
            },
          },
        },
        renderer: {},
      }),
    ],
    resolve: {
      alias: { '@': path.resolve(__dirname, 'src') },
    },
    clearScreen: false,
  }
})
