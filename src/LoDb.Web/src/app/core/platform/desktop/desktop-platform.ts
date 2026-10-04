import { DOCUMENT, Injectable, type Signal, inject } from '@angular/core';
import { AuthStrategies } from '../../auth/strategy/auth-strategies';
import type { AuthStrategyKind } from '../auth-strategy-kind';
import type { PlatformKind } from '../platform-kind';
import type { PlatformService } from '../platform-service';
import type { ShareOutcome } from '../share-outcome';
import type { UpdateState } from '../update-state';
import { HostAuthStrategy } from './auth/host-auth-strategy';
import { BridgeClient } from './bridge/bridge-client';
import { saveFilePayload } from './files/file-payload';
import { ExternalLinks } from './links/external-links';
import { DESKTOP_MARKER } from './marker/desktop-marker-token';
import { DesktopUpdates } from './updates/desktop-updates';

const WEB_SCHEMES = ['http:', 'https:'];
// The user picks where to save in the host's native dialog, which can stay open a while.
const SAVE_TIMEOUT_MS = 5 * 60_000;

function isWebUrl(url: string): boolean {
  try {
    return WEB_SCHEMES.includes(new URL(url).protocol);
  } catch {
    return false;
  }
}

/**
 * The Photino desktop app (ADR 0004): pages and API on the host's loopback origin, the host
 * relaying `/api` with the tokens it holds (`host` strategy), links and files through its
 * bridge, updates by Velopack. Built once by the detection, which loads this file lazily.
 * Nothing is kept in the page's storage: the host keeps what must outlive a restart.
 */
@Injectable({ providedIn: 'root' })
export class DesktopPlatform implements PlatformService {
  readonly kind: PlatformKind = 'desktop';
  readonly authStrategy: AuthStrategyKind = 'host';
  private readonly document = inject(DOCUMENT);
  private readonly version = inject(DESKTOP_MARKER)?.version ?? null;
  private readonly bridge = inject(BridgeClient);
  private readonly updates = inject(DesktopUpdates);
  readonly updateState: Signal<UpdateState> = this.updates.state;

  // In place before the first navigation, which the detection precedes. Nothing built here
  // may read `API_BASE_URL`: it comes from `PLATFORM`, not yet set.
  constructor() {
    inject(AuthStrategies).register('host', HostAuthStrategy);
    if (this.bridge.isAvailable) {
      inject(ExternalLinks).intercept((url) => this.openExternal(url));
      this.updates.start();
    }
  }

  /** The host relays `/api` from its own origin, which the page shares. */
  apiOrigin(): string {
    return this.document.location.origin;
  }

  /**
   * `desktop/{version}` (ADR 0008). The host's proxy sets it again on what it relays; the
   * page sends it too, for the requests that would reach the API some other way.
   */
  clientHeader(): string | null {
    return this.version === null ? null : `desktop/${this.version}`;
  }

  async openExternal(url: string): Promise<void> {
    if (!isWebUrl(url)) {
      throw new Error('Only http(s) links can be opened outside the application.');
    }
    await this.bridge.request('openExternal', { url });
  }

  /** The host's save dialog; closing it without saving is not a failure. */
  async saveFile(file: File): Promise<void> {
    await this.bridge.request('saveFile', await saveFilePayload(file), SAVE_TIMEOUT_MS);
  }

  /** The WebView has no share sheet: the link is copied, which the caller words as such. */
  async share(content: ShareData): Promise<ShareOutcome> {
    const clipboard = this.document.defaultView?.navigator.clipboard;
    if (!content.url || !clipboard) {
      throw new Error('Only a link can be shared from the desktop app, by copying it.');
    }
    await clipboard.writeText(content.url);
    return 'copied';
  }

  applyUpdate(): Promise<void> {
    return this.updates.apply();
  }
}
