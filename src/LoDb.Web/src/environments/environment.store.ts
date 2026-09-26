import type { AppEnvironment } from './app-environment';
import { environment as shell } from './environment.shell';

/**
 * Store variant of the shell build (`shell-store`), embedded by the Android app: the shell
 * without any payment screen, as Play requires (ADR 0007).
 */
export const environment: AppEnvironment = {
  ...shell,
  payments: false,
};
