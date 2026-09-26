import { DestroyRef, Injectable, type Signal, inject, signal } from '@angular/core';
import { BridgeClient } from '../bridge/bridge-client';
import type { UpdateState } from '../../update-state';
import { updateStateOf } from './update-state-of';

/**
 * How often the page asks the host where its update stands. Velopack checks the feed and
 * downloads in the background far less often: a minute of delay before the banner shows
 * costs nothing, and an idle bridge costs the host nothing either.
 */
export const UPDATE_POLL_MS = 60_000;
const UPDATE_STATE = 'updateState';
const APPLY_UPDATE = 'applyUpdate';

/**
 * The update state of the desktop app (Velopack, L9.2), for `core/update`: read from the
 * host now, then every minute until an update is ready, which it then stays until the app
 * restarts on it. The host pushes nothing: the bridge only answers.
 */
@Injectable({ providedIn: 'root' })
export class DesktopUpdates {
  private readonly bridge = inject(BridgeClient);
  private readonly current = signal<UpdateState>('none');
  private timer: ReturnType<typeof setInterval> | null = null;

  readonly state: Signal<UpdateState> = this.current.asReadonly();

  constructor() {
    inject(DestroyRef).onDestroy(() => this.stop());
  }

  /** Starts following the host; without a bridge the app never announces an update. */
  start(): void {
    if (!this.bridge.isAvailable || this.timer !== null) {
      return;
    }
    this.timer = setInterval(() => void this.refresh(), UPDATE_POLL_MS);
    void this.refresh();
  }

  /** Restarts on the ready update; rejects when the host has none to apply. */
  async apply(): Promise<void> {
    await this.bridge.request(APPLY_UPDATE);
  }

  // A failed read keeps the last state: the next one may succeed.
  private async refresh(): Promise<void> {
    const state = await this.bridge
      .request(UPDATE_STATE)
      .then(updateStateOf)
      .catch(() => null);
    if (state === null) {
      return;
    }
    this.current.set(state);
    if (state === 'ready') {
      this.stop();
    }
  }

  private stop(): void {
    if (this.timer !== null) {
      clearInterval(this.timer);
      this.timer = null;
    }
  }
}
