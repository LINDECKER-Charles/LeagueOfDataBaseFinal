import type { AppUpdatePlugin } from '@capawesome/capacitor-app-update';
import type { LiveUpdatePlugin } from '@capawesome/capacitor-live-update';

/**
 * The part of the update plugins the Android app uses (ADR 0008): the live update of the
 * front (level 1) and Play In-App Updates of the native shell (level 2). Kept this narrow so
 * a spec can simulate both without a device.
 */
export interface UpdatePlugins {
  readonly liveUpdate: Pick<
    LiveUpdatePlugin,
    | 'ready'
    | 'getCurrentBundle'
    | 'getNextBundle'
    | 'getDownloadedBundles'
    | 'downloadBundle'
    | 'setNextBundle'
    | 'deleteBundle'
    | 'reload'
  >;
  readonly appUpdate: Pick<
    AppUpdatePlugin,
    | 'getAppUpdateInfo'
    | 'performImmediateUpdate'
    | 'startFlexibleUpdate'
    | 'completeFlexibleUpdate'
    | 'openAppStore'
    | 'addListener'
  >;
}
