import { DOCUMENT, Injectable, Injector, PLATFORM_ID, inject } from '@angular/core';
import { isPlatformBrowser } from '@angular/common';
import type { PlatformKind } from '../platform-kind';
import type { PlatformService } from '../platform-service';
import { WebPlatform } from '../web/web-platform';
import { detectPlatformKind } from './detect-platform-kind';

/**
 * Holds the implementation chosen once at startup. Desktop and Android implementations are
 * loaded lazily, so their host bridges and native plugins never weigh on the web bundle.
 */
@Injectable({ providedIn: 'root' })
export class ActivePlatform {
  private readonly injector = inject(Injector);
  private readonly globals = isPlatformBrowser(inject(PLATFORM_ID))
    ? inject(DOCUMENT).defaultView
    : null;
  private detection: Promise<PlatformService> | null = null;
  private current: PlatformService | null = null;

  /** Idempotent: startup code that needs the platform awaits the same detection. */
  detect(): Promise<PlatformService> {
    this.detection ??= this.load(detectPlatformKind(this.globals)).then((platform) => {
      this.current = platform;
      return platform;
    });
    return this.detection;
  }

  get(): PlatformService {
    if (this.current === null) {
      throw new Error('PLATFORM was injected before the platform detection completed.');
    }
    return this.current;
  }

  private async load(kind: PlatformKind): Promise<PlatformService> {
    switch (kind) {
      case 'desktop':
        return this.injector.get((await import('../desktop/desktop-platform')).DesktopPlatform);
      case 'android':
        return this.injector.get((await import('../android/android-platform')).AndroidPlatform);
      default:
        return this.injector.get(WebPlatform);
    }
  }
}
