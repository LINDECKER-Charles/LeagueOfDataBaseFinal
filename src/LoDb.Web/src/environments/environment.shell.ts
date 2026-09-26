import type { AppEnvironment } from './app-environment';

/** Shell build, embedded by the desktop host. Its store variant is environment.store.ts. */
export const environment: AppEnvironment = {
  publicApiOrigin: 'https://league-of-data-base.com',
  payments: true,
};
