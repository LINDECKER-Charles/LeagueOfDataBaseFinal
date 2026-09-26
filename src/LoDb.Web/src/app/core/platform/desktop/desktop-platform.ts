import { Injectable } from '@angular/core';
import type { PlatformKind } from '../platform-kind';
import { WebPlatform } from '../web/web-platform';

/**
 * Placeholder of the Photino desktop platform: it behaves as the web until L9.3 implements the
 * host bridge (links, files, updates, `host` authentication). Detection loads this class by
 * path and name, so L9.3 replaces this file without touching `core/platform/detection`.
 */
@Injectable({ providedIn: 'root' })
export class DesktopPlatform extends WebPlatform {
  override readonly kind: PlatformKind = 'desktop';
}
