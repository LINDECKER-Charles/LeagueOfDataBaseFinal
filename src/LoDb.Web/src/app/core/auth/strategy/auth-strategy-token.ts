import { InjectionToken, inject } from '@angular/core';
import { PLATFORM } from '../../platform/platform';
import type { AuthStrategy } from './auth-strategy';
import { AuthStrategies } from './auth-strategies';

/**
 * The strategy of the platform detected at startup (`PlatformService.authStrategy`). Like
 * `PLATFORM`, it can only be injected once the detection has completed, which every guard,
 * page and API request comes after.
 */
export const AUTH_STRATEGY = new InjectionToken<AuthStrategy>('AUTH_STRATEGY', {
  providedIn: 'root',
  factory: () => inject(AuthStrategies).get(inject(PLATFORM).authStrategy),
});
