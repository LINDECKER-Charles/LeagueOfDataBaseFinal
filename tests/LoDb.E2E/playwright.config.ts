import { defineConfig, devices } from '@playwright/test';

// The integration stack (plan, section 6.3). A slot points the suite at its own nginx port,
// for instance LODB_E2E_BASE_URL=http://localhost:18180 for slot 1.
const DEFAULT_BASE_URL = 'http://localhost:18080';
// Locally the suite shares the machine with the stacks it tests: a few workers suffice.
const LOCAL_WORKERS = 4;
const CI_WORKERS = 2;

const onCi = process.env['CI'] !== undefined;

export default defineConfig({
  testDir: './specs',
  fullyParallel: true,
  forbidOnly: onCi,
  retries: onCi ? 1 : 0,
  workers: onCi ? CI_WORKERS : LOCAL_WORKERS,
  reporter: onCi ? [['list'], ['html', { open: 'never' }]] : 'list',
  use: {
    baseURL: process.env['LODB_E2E_BASE_URL'] ?? DEFAULT_BASE_URL,
    trace: 'retain-on-failure',
  },
  projects: [{ name: 'chromium', use: { ...devices['Desktop Chrome'] } }],
});
