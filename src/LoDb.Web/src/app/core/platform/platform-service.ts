import type { Signal } from '@angular/core';
import type { AuthStrategyKind } from './auth-strategy-kind';
import type { PlatformKind } from './platform-kind';
import type { ShareOutcome } from './share-outcome';
import type { UpdateState } from './update-state';

/**
 * Everything that differs between the web, desktop and Android targets (plan, section 5.2).
 * Code outside `core/platform` goes through this interface, never through a native plugin.
 */
export interface PlatformService {
  readonly kind: PlatformKind;
  readonly authStrategy: AuthStrategyKind;
  readonly updateState: Signal<UpdateState>;

  /** Origin of the API, without path: the generated client appends `/api/...` itself. */
  apiOrigin(): string;

  /** Value of the `X-LoDb-Client` header (L3.1), or null to send none, as on the web. */
  clientHeader(): string | null;

  /** Opens an http(s) link outside the application. Rejects any other scheme. */
  openExternal(url: string): Promise<void>;

  saveFile(file: File): Promise<void>;

  share(content: ShareData): Promise<ShareOutcome>;

  /** Restarts on the update announced by `updateState() === 'ready'`. */
  applyUpdate(): Promise<void>;
}
