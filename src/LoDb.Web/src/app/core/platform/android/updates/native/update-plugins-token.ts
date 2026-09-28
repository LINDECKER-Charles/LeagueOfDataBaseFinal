import { InjectionToken } from '@angular/core';
import { AppUpdate } from '@capawesome/capacitor-app-update';
import { LiveUpdate } from '@capawesome/capacitor-live-update';
import type { UpdatePlugins } from './update-plugins';

/**
 * The update plugins, through one token that specs replace. Only the Android platform, which
 * the detection loads lazily, reaches this file: the plugins never weigh on the web bundle.
 */
export const UPDATE_PLUGINS = new InjectionToken<UpdatePlugins>('UPDATE_PLUGINS', {
  providedIn: 'root',
  factory: () => ({ liveUpdate: LiveUpdate, appUpdate: AppUpdate }),
});
