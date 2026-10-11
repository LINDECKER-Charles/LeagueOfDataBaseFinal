import { DOCUMENT, Injectable, type Signal, inject, signal } from '@angular/core';
import type { AuthStrategyKind } from '../auth-strategy-kind';
import { isHttpsUrl } from '../is-https-url';
import type { PlatformKind } from '../platform-kind';
import type { PlatformService } from '../platform-service';
import type { ShareOutcome } from '../share-outcome';
import type { UpdateState } from '../update-state';

// Some browsers read the object URL after `click()` returns: revoking it at once can cancel
// the download, keeping it a minute costs nothing.
const OBJECT_URL_LIFETIME_MS = 60_000;

function isDismissal(error: unknown): boolean {
  return error instanceof DOMException && error.name === 'AbortError';
}

/** The browser: same-origin API, session cookie, updates by reloading the page. */
@Injectable({ providedIn: 'root' })
export class WebPlatform implements PlatformService {
  readonly kind: PlatformKind = 'web';
  readonly authStrategy: AuthStrategyKind = 'cookie';
  readonly updateState: Signal<UpdateState> = signal<UpdateState>('none').asReadonly();
  protected readonly document = inject(DOCUMENT);

  apiOrigin(): string {
    return this.document.location.origin;
  }

  clientHeader(): string | null {
    return null;
  }

  async openExternal(url: string): Promise<void> {
    if (!isHttpsUrl(url)) {
      throw new Error('Only https links can be opened outside the application.');
    }
    this.document.defaultView?.open(url, '_blank', 'noopener,noreferrer');
  }

  async saveFile(file: File): Promise<void> {
    const link = this.document.createElement('a');
    link.href = URL.createObjectURL(file);
    link.download = file.name;
    link.click();
    setTimeout(() => URL.revokeObjectURL(link.href), OBJECT_URL_LIFETIME_MS);
  }

  async share(content: ShareData): Promise<ShareOutcome> {
    const navigator = this.document.defaultView?.navigator;
    if (navigator?.share) {
      try {
        await navigator.share(content);
        return 'shared';
      } catch (error) {
        if (isDismissal(error)) {
          return 'dismissed';
        }
        throw error;
      }
    }
    if (content.url && navigator?.clipboard) {
      await navigator.clipboard.writeText(content.url);
      return 'copied';
    }
    throw new Error('This browser can neither share nor copy a link.');
  }

  async applyUpdate(): Promise<void> {
    // Nothing to apply: a web page is always the latest deployment once reloaded.
  }
}
