import { defineConfig } from 'vitest/config';

// Vitest starts one jsdom worker per logical CPU by default: two dozen on a workstation,
// close to 5 GB for one run, for barely a faster one than with four.
export default defineConfig({
  test: {
    maxWorkers: 4,
  },
});
