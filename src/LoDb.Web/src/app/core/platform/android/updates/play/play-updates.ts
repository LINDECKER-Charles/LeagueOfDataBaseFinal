import { Injectable, type Signal, inject, signal } from '@angular/core';
import {
  type AppUpdateInfo,
  AppUpdateAvailability,
  AppUpdateResultCode,
} from '@capawesome/capacitor-app-update';
import type { UpdateState } from '../../../update-state';
import { UPDATE_PLUGINS } from '../native/update-plugins-token';
import type { NativeStep } from '../plan/native-step';
import { installStateOf } from './install-state-of';

// Play also reports an immediate update the user left before it finished: it resumes.
function isOffered({ updateAvailability }: AppUpdateInfo): boolean {
  return (
    updateAvailability === AppUpdateAvailability.UPDATE_AVAILABLE ||
    updateAvailability === AppUpdateAvailability.UPDATE_IN_PROGRESS
  );
}

/**
 * Level 2 of ADR 0008: the native shell updates through Play In-App Updates, flexible by
 * default (downloaded in the background, installed on restart), immediate below the minimum
 * of the client policy (Play covers the app until the new version runs).
 */
@Injectable({ providedIn: 'root' })
export class PlayUpdates {
  private readonly plugin = inject(UPDATE_PLUGINS).appUpdate;
  private readonly current = signal<UpdateState>('none');
  private isListening = false;
  // A flexible offer the user turned down is not made again for the same version.
  private offeredVersionCode: string | null = null;

  readonly state: Signal<UpdateState> = this.current.asReadonly();

  /**
   * Runs the native step through Play. False when Play does not manage this installation
   * (installed outside Play, no Play services), for the transitional channel to take over.
   */
  async run(step: NativeStep): Promise<boolean> {
    const info = await this.plugin.getAppUpdateInfo().catch(() => null);
    if (info === null || info.updateAvailability === AppUpdateAvailability.UNKNOWN) {
      return false;
    }
    this.listen();
    this.current.set(installStateOf(info.installStatus));
    if (step === 'immediate') {
      await this.immediate(info);
    } else if (step === 'flexible') {
      await this.flexible(info);
    }
    return true;
  }

  /** Restarts the app on the downloaded version: Play installs it. */
  async apply(): Promise<void> {
    await this.plugin.completeFlexibleUpdate();
  }

  // Play may not serve the new version to this device yet: the next check asks again.
  private async immediate(info: AppUpdateInfo): Promise<void> {
    if (!isOffered(info)) {
      return;
    }
    if (info.immediateUpdateAllowed) {
      await this.plugin.performImmediateUpdate();
    } else {
      await this.plugin.openAppStore();
    }
  }

  private async flexible(info: AppUpdateInfo): Promise<void> {
    const versionCode = info.availableVersionCode ?? null;
    const offerable =
      info.updateAvailability === AppUpdateAvailability.UPDATE_AVAILABLE &&
      info.flexibleUpdateAllowed === true &&
      versionCode !== this.offeredVersionCode;
    if (!offerable) {
      return;
    }
    this.offeredVersionCode = versionCode;
    const { code } = await this.plugin.startFlexibleUpdate();
    if (code === AppUpdateResultCode.OK) {
      this.current.set('downloading');
    }
  }

  private listen(): void {
    if (this.isListening) {
      return;
    }
    this.isListening = true;
    void this.plugin.addListener('onFlexibleUpdateStateChange', ({ installStatus }) =>
      this.current.set(installStateOf(installStatus)),
    );
  }
}
