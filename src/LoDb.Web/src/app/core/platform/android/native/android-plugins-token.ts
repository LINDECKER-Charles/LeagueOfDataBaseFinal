import { SecureStorage } from '@aparajita/capacitor-secure-storage';
import { App } from '@capacitor/app';
import { Browser } from '@capacitor/browser';
import { Share } from '@capacitor/share';
import { InjectionToken } from '@angular/core';
import type { AndroidPlugins } from './android-plugins';

/**
 * The native plugins, through one token that specs replace. Only the Android platform, which
 * the detection loads lazily, reaches this file: the plugins never weigh on the web bundle.
 */
export const ANDROID_PLUGINS = new InjectionToken<AndroidPlugins>('ANDROID_PLUGINS', {
  providedIn: 'root',
  factory: () => ({ app: App, browser: Browser, share: Share, secureStorage: SecureStorage }),
});
