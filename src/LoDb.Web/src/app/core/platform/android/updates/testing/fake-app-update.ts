import type { PluginListenerHandle } from '@capacitor/core';
import {
  type AppUpdateInfo,
  AppUpdateAvailability,
  AppUpdateResultCode,
  FlexibleUpdateInstallStatus,
  type FlexibleUpdateState,
} from '@capawesome/capacitor-app-update';
import type { UpdatePlugins } from '../native/update-plugins';

type StateListener = (state: FlexibleUpdateState) => void;

/**
 * Play In-App Updates, simulated: a spec sets what Play reports (`info`, or `unmanaged` for
 * an installation from outside Play), answers the flows, and fires the download progress.
 * Like Play, `info` reports a flexible update from its start on.
 */
export class FakeAppUpdate {
  info: AppUpdateInfo = {
    currentVersionName: '2.3.0',
    currentVersionCode: '2003000',
    updateAvailability: AppUpdateAvailability.UPDATE_NOT_AVAILABLE,
  };
  unmanaged = false;
  flexibleAnswer = AppUpdateResultCode.OK;
  immediateUpdates = 0;
  flexibleUpdates = 0;
  completions = 0;
  storeOpenings = 0;
  readonly listeners: StateListener[] = [];

  readonly plugin: UpdatePlugins['appUpdate'] = {
    getAppUpdateInfo: async () => {
      if (this.unmanaged) {
        throw new Error('Install Error(-10): The app is not owned by any user on this device.');
      }
      return this.info;
    },
    performImmediateUpdate: async () => {
      this.immediateUpdates++;
      return { code: AppUpdateResultCode.OK };
    },
    startFlexibleUpdate: async () => {
      this.flexibleUpdates++;
      if (this.flexibleAnswer === AppUpdateResultCode.OK) {
        this.info = { ...this.info, installStatus: FlexibleUpdateInstallStatus.PENDING };
      }
      return { code: this.flexibleAnswer };
    },
    completeFlexibleUpdate: async () => {
      this.completions++;
    },
    openAppStore: async () => {
      this.storeOpenings++;
    },
    addListener: async (_eventName: string, listener: StateListener) => {
      this.listeners.push(listener);
      return { remove: async () => undefined } satisfies PluginListenerHandle;
    },
  };

  /** Play reports a new version, allowed in both flows unless the spec says otherwise. */
  offer(availableVersionCode: string, allowed: Partial<AppUpdateInfo> = {}): void {
    this.info = {
      ...this.info,
      availableVersionCode,
      updateAvailability: AppUpdateAvailability.UPDATE_AVAILABLE,
      immediateUpdateAllowed: true,
      flexibleUpdateAllowed: true,
      ...allowed,
    };
  }

  /** Fires the progress of a flexible update, which Play then also reports in `info`. */
  emit(state: FlexibleUpdateState): void {
    this.info = { ...this.info, installStatus: state.installStatus };
    for (const listener of this.listeners) {
      listener(state);
    }
  }
}
