import type { AppEnvironment } from './app-environment';

/** Web build: pages and API share one origin behind nginx. */
export const environment: AppEnvironment = {
  publicApiOrigin: null,
  payments: true,
};
