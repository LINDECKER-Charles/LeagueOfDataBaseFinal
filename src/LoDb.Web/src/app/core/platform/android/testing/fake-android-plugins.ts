import type { PluginListenerHandle } from '@capacitor/core';
import type { OpenOptions } from '@capacitor/browser';
import type { ShareOptions } from '@capacitor/share';
import type { AndroidPlugins } from '../native/android-plugins';

type Listener = (event: never) => void;

/**
 * The Capacitor plugins of the Android platform, simulated: a spec fires the native events
 * (`emit`), reads what was opened, shared and stored, and sets what the plugins answer.
 */
export class FakeAndroidPlugins {
  readonly listeners = new Map<string, Listener[]>();
  readonly opened: string[] = [];
  readonly shared: ShareOptions[] = [];
  readonly stored = new Map<string, string>();
  browserCloses = 0;
  minimizes = 0;
  version = '2.3.0';
  launchUrl: string | null = null;
  shareAnswer: () => Promise<void> = async () => undefined;
  storageFailure: Error | null = null;

  readonly plugins: AndroidPlugins = {
    app: {
      addListener: ((eventName: string, listener: Listener) =>
        this.listen(eventName, listener)) as AndroidPlugins['app']['addListener'],
      getInfo: async () => ({
        name: 'LeagueOfDataBase',
        id: 'app',
        build: '7',
        version: this.version,
      }),
      getLaunchUrl: async () => (this.launchUrl === null ? undefined : { url: this.launchUrl }),
      minimizeApp: async () => {
        this.minimizes++;
      },
    },
    browser: {
      open: async ({ url }: OpenOptions) => {
        this.opened.push(url);
      },
      close: async () => {
        this.browserCloses++;
      },
    },
    share: {
      share: async (options: ShareOptions) => {
        this.shared.push(options);
        await this.shareAnswer();
        return {};
      },
    },
    secureStorage: {
      getItem: async (key: string) => this.storage().get(key) ?? null,
      setItem: async (key: string, value: string) => {
        this.storage().set(key, value);
      },
      removeItem: async (key: string) => {
        this.storage().delete(key);
      },
    },
  };

  /** Fires a native event at every listener of `eventName`. */
  emit(eventName: string, event?: unknown): void {
    for (const listener of this.listeners.get(eventName) ?? []) {
      (listener as (value: unknown) => void)(event);
    }
  }

  private listen(eventName: string, listener: Listener): Promise<PluginListenerHandle> {
    this.listeners.set(eventName, [...(this.listeners.get(eventName) ?? []), listener]);
    return Promise.resolve({ remove: async () => undefined });
  }

  private storage(): Map<string, string> {
    if (this.storageFailure !== null) {
      throw this.storageFailure;
    }
    return this.stored;
  }
}
