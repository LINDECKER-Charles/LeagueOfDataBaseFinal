import { Injectable } from '@angular/core';
import { environment } from '../../../../environments/environment';
import type { PlatformKind } from '../platform-kind';
import { WebPlatform } from '../web/web-platform';

/**
 * Placeholder of the Capacitor Android platform: it behaves as the web, except for the API
 * origin, until L10.2 implements the plugins and the `bearer` authentication. Detection loads
 * this class by path and name, so L10.2 replaces this file without touching the detection.
 */
@Injectable({ providedIn: 'root' })
export class AndroidPlatform extends WebPlatform {
  override readonly kind: PlatformKind = 'android';

  // The WebView serves the shell from https://localhost: the API is on its public origin.
  override apiOrigin(): string {
    return environment.publicApiOrigin ?? super.apiOrigin();
  }
}
