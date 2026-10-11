import { LocationStrategy, isPlatformBrowser } from '@angular/common';
import { DOCUMENT, Injectable, PLATFORM_ID, inject } from '@angular/core';

// The address of the last reload, for this tab only. A second failure at the same address in
// a row means the bundle is really missing: the page must not reload forever.
const RELOADED_KEY = 'lodb.chunk-reload';

/**
 * Recovers a tab opened before a deployment, whose lazy route names a bundle that failed to
 * load: the whole page is loaded again once, and then names the bundles of the new build.
 */
@Injectable({ providedIn: 'root' })
export class ChunkReload {
  private readonly document = inject(DOCUMENT);
  private readonly locationStrategy = inject(LocationStrategy);
  private readonly inBrowser = isPlatformBrowser(inject(PLATFORM_ID));

  /**
   * Loads the page at the router's `url` again, unless the previous reload of this tab was
   * for the same address, or the tab cannot remember it (server, storage refused): false
   * then, and the caller shows its error page. That failure forgets the address, so a later
   * attempt of the visitor gets its own reload.
   */
  reloadOnce(url: string): boolean {
    const storage = this.session();
    if (storage === null || !this.claim(storage, url)) {
      return false;
    }
    this.document.location.assign(this.locationStrategy.prepareExternalUrl(url));
    return true;
  }

  private claim(storage: Storage, url: string): boolean {
    try {
      if (storage.getItem(RELOADED_KEY) === url) {
        storage.removeItem(RELOADED_KEY);
        return false;
      }
      storage.setItem(RELOADED_KEY, url);
      return true;
    } catch {
      // Storage refused (private mode, quota): without the guard, a reload could loop.
      return false;
    }
  }

  private session(): Storage | null {
    try {
      return this.inBrowser ? (this.document.defaultView?.sessionStorage ?? null) : null;
    } catch {
      // Sandboxed documents throw on storage access.
      return null;
    }
  }
}
