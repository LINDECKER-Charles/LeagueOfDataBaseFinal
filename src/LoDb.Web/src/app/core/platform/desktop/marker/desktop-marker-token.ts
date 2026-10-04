import { DOCUMENT, InjectionToken, inject } from '@angular/core';
import type { DesktopMarker } from './desktop-marker';
import { readDesktopMarker } from './read-desktop-marker';

/**
 * The marker of the desktop host, read once from the page. The desktop platform is only
 * built once the detection found it, so it is never null there; specs provide their own.
 */
export const DESKTOP_MARKER = new InjectionToken<DesktopMarker | null>('DESKTOP_MARKER', {
  providedIn: 'root',
  factory: () => readDesktopMarker(inject(DOCUMENT).defaultView),
});
