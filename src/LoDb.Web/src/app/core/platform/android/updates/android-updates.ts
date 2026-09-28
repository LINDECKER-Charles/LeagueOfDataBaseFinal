import { Injectable, Injector, type Signal, computed, effect, inject } from '@angular/core';
import { ClientUpdate } from '../../../update/client-update';
import type { UpdateState } from '../../update-state';
import { AndroidLifecycle } from '../lifecycle/android-lifecycle';
import { AppStart } from './check/app-start';
import { CheckSchedule } from './check/check-schedule';
import { UpdateCheck } from './check/update-check';
import { LiveUpdates } from './live/live-updates';
import { mostAdvancedState } from './most-advanced-state';
import { PlayUpdates } from './play/play-updates';
import { TransitionalChannel } from './transitional/transitional-channel';

/**
 * The updates of the Android app (ADR 0008), for `PlatformService.updateState` and
 * `applyUpdate`: the front by signed live update bundles, rolled back when they fail to
 * start; the shell by Play In-App Updates, or by the transitional channel outside Play.
 * Checks at start, on each return to the foreground (at most every 15 minutes), and at once
 * when the API refuses the app with a 426.
 */
@Injectable({ providedIn: 'root' })
export class AndroidUpdates {
  private readonly injector = inject(Injector);
  private readonly live = inject(LiveUpdates);
  private readonly play = inject(PlayUpdates);
  private readonly transitional = inject(TransitionalChannel);
  private readonly schedule = new CheckSchedule();
  private checking: Promise<void> | null = null;
  private isStarted = false;
  private isBlocked = false;

  readonly state: Signal<UpdateState> = computed(() =>
    mostAdvancedState([this.play.state(), this.live.state(), this.transitional.state()]),
  );

  /** Idempotent: the platform starts it once, at detection. Needs no API origin yet. */
  start(): void {
    if (this.isStarted) {
      return;
    }
    this.isStarted = true;
    void this.follow();
  }

  /** Restarts on the ready update; a native one also brings the front it embeds. */
  async apply(): Promise<void> {
    const sources = [this.play, this.live, this.transitional];
    await sources.find((source) => source.state() === 'ready')?.apply();
  }

  private async follow(): Promise<void> {
    await this.injector.get(AppStart).whenStarted();
    // Before any change of bundle, as the plugin requires; its failure must not stop checks.
    await this.live.confirmStart().catch(() => undefined);
    this.injector.get(AndroidLifecycle).resumes.subscribe(() => void this.check(false));
    this.checkOnceBlocked();
    await this.check(true);
  }

  private checkOnceBlocked(): void {
    const requirement = this.injector.get(ClientUpdate).requirement;
    effect(
      () => {
        if (requirement() !== null && !this.isBlocked) {
          this.isBlocked = true;
          void this.check(true);
        }
      },
      { injector: this.injector },
    );
  }

  // One check at a time; a forced one (start, 426) ignores the 15 minutes.
  private check(force: boolean): Promise<void> {
    const now = Date.now();
    if (this.checking === null && (force || this.schedule.isDue(now))) {
      this.schedule.record(now);
      this.checking = this.injector
        .get(UpdateCheck)
        .run()
        .catch(() => undefined)
        .finally(() => {
          this.checking = null;
        });
    }
    return this.checking ?? Promise.resolve();
  }
}
