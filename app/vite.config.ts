import { defineConfig } from 'vite'
import vue from '@vitejs/plugin-vue'
import tailwindcss from '@tailwindcss/vite'
import symfony from 'vite-plugin-symfony'

export default defineConfig({
    plugins: [
        vue(),
        tailwindcss(),
        symfony(),
    ],
    build: {
        manifest: true,
        rollupOptions: {
            input: {
                app: './assets/vue/main.ts',
            },
        },
    },
})
