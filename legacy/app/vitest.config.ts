import { defineConfig } from 'vitest/config'
import vue from '@vitejs/plugin-vue'

// Standalone from vite.config.ts: the Symfony/Vite bundle plugin is build-only
// and must not run under the test harness.
export default defineConfig({
  plugins: [vue()],
  test: {
    environment: 'jsdom',
    // tests/js covers the admin panel's static ES modules (public/admin/js),
    // which live outside the Vite build on purpose.
    include: ['assets/vue/**/*.spec.ts', 'tests/js/**/*.spec.js'],
  },
})
