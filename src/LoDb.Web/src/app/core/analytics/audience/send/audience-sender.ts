import { DOCUMENT, Injectable, Injector, inject } from '@angular/core';
import { PLATFORM } from '../../../platform/platform';
import type { AudienceDevice } from '../audience-device';
import type { AudienceHit } from '../audience-hit';
import { AUDIENCE_SITE } from '../audience-site';
import { AudienceOptOut } from './audience-opt-out';
import { umamiBodyOf } from './umami-body-of';

const SEND_PATH = '/u/api/send';
const CACHE_HEADER = 'x-umami-cache';
const JSON_TYPE = 'application/json';
const SCREEN_SEPARATOR = 'x';

interface SendReply {
  readonly cache?: unknown;
}

/**
 * Posts the hits to Umami through the site's own origin (`/u/api/send`, routed by the edge),
 * so the CSP keeps `connect-src 'self'` and no script of Umami ever runs in the page.
 *
 * Raw `fetch`, not HttpClient: the interceptors would add the session's headers, and the
 * apps' bearer token. No credentials, and `keepalive` so a hit outlives the page it leaves.
 * No referrer either: a same-origin request carries the page's full address in its `Referer`
 * header (a shared build's token, a pseudonym, a search), past the normalization, and the
 * edge keeps that header in its access log. Umami's `cache` token, which spares it a session
 * lookup, lives in memory only.
 *
 * Sends only from the site's canonical host (staging, local stacks and end-to-end runs share
 * the bundle and stay silent), on the web (the apps come later), and while the visitor is
 * measured. A failure is dropped: measuring never troubles the page.
 */
@Injectable({ providedIn: 'root' })
export class AudienceSender {
  private readonly document = inject(DOCUMENT);
  private readonly injector = inject(Injector);
  private readonly optOut = inject(AudienceOptOut);
  private cache: string | null = null;

  /** Whether hits leave this page at all. */
  isActive(): boolean {
    const platform = this.injector.get(PLATFORM);
    return (
      platform.kind === 'web' &&
      this.document.location?.hostname === AUDIENCE_SITE.hostname &&
      this.optOut.isMeasured()
    );
  }

  send(hit: AudienceHit): void {
    if (this.isActive()) {
      void this.post(hit);
    }
  }

  private async post(hit: AudienceHit): Promise<void> {
    const platform = this.injector.get(PLATFORM);
    const headers: Record<string, string> = { 'Content-Type': JSON_TYPE };
    if (this.cache !== null) {
      headers[CACHE_HEADER] = this.cache;
    }
    try {
      const response = await fetch(`${platform.apiOrigin()}${SEND_PATH}`, {
        method: 'POST',
        headers,
        body: JSON.stringify(umamiBodyOf(hit, this.device())),
        credentials: 'omit',
        referrerPolicy: 'no-referrer',
        keepalive: true,
      });
      this.keepCache(response.ok ? ((await response.json()) as SendReply) : null);
    } catch {
      // Offline, blocked by an extension, or Umami down: the hit is lost, the page goes on.
    }
  }

  private keepCache(reply: SendReply | null): void {
    if (typeof reply?.cache === 'string') {
      this.cache = reply.cache;
    }
  }

  private device(): AudienceDevice {
    const view = this.document.defaultView;
    const screen = view?.screen;
    return {
      language: view?.navigator.language ?? '',
      screen: screen ? `${screen.width}${SCREEN_SEPARATOR}${screen.height}` : '',
      platform: this.injector.get(PLATFORM).kind,
    };
  }
}
