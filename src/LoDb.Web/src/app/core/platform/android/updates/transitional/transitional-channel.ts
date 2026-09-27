import { HttpBackend, HttpClient } from '@angular/common/http';
import { Injectable, Injector, type Signal, computed, inject, signal } from '@angular/core';
import { firstValueFrom } from 'rxjs';
import type { UpdateState } from '../../../update-state';
import { ANDROID_PLUGINS } from '../../native/android-plugins-token';
import type { ApkManifest } from './apk-manifest';
import { parseApkManifest } from './parse-apk-manifest';
import { TRANSITIONAL_CHANNEL } from './transitional-channel-token';

/**
 * The transitional channel of ADR 0008, for installations Play does not manage, until the
 * Play listing opens: `latest.json` names the newest APK, offered when its `versionCode` is
 * above the installed one, and opened in the system browser. Android installs it once the
 * user confirms, and only over an app signed with the same key: the key Play App Signing
 * imports later. The app never installs anything itself.
 */
@Injectable({ providedIn: 'root' })
export class TransitionalChannel {
  private readonly settings = inject(TRANSITIONAL_CHANNEL);
  private readonly injector = inject(Injector);
  private readonly plugins = inject(ANDROID_PLUGINS);
  private readonly offer = signal<ApkManifest | null>(null);

  /** `ready` while a newer APK is offered: applying it opens the download. */
  readonly state: Signal<UpdateState> = computed(() => (this.offer() === null ? 'none' : 'ready'));

  /** Reads the manifest; a failed read keeps the last offer. Does nothing when off. */
  async run(installedVersionCode: number): Promise<void> {
    if (this.settings === null || !Number.isSafeInteger(installedVersionCode)) {
      return;
    }
    // Without the interceptors: the manifest is no API resource and needs no credentials.
    const http = new HttpClient(this.injector.get(HttpBackend));
    const body = await firstValueFrom(http.get<unknown>(this.settings.manifestUrl)).catch(
      () => null,
    );
    if (body === null) {
      return;
    }
    const manifest = parseApkManifest(body, this.settings.apkHosts);
    this.offer.set(
      manifest !== null && manifest.versionCode > installedVersionCode ? manifest : null,
    );
  }

  /** Opens the offered APK in the system browser. */
  async apply(): Promise<void> {
    const offer = this.offer();
    if (offer !== null) {
      await this.plugins.browser.open({ url: offer.url });
    }
  }
}
