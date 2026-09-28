import { InjectionToken } from '@angular/core';

/**
 * Release of the application shown by the header chip, which links to the changelog. The
 * changelog chantier (L3.10) provides the newest entry of its manifest; until then the token
 * answers null and the chip is not rendered.
 */
export const RELEASE_VERSION = new InjectionToken<string | null>('RELEASE_VERSION', {
  providedIn: 'root',
  factory: () => null,
});
