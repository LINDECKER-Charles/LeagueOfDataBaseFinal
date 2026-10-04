import type { DownloadBundleOptions } from '@capawesome/capacitor-live-update';
import type { UpdatePlugins } from '../native/update-plugins';

/**
 * The live update plugin, simulated with its native rules (@capawesome/capacitor-live-update
 * 8, LiveUpdate.java): the next bundle becomes the current one at a cold start or a reload,
 * and a start that does not call `ready()` before the timeout is rolled back to the embedded
 * bundle (null). Outlives a TestBed, as the device outlives the app's process.
 */
export class FakeLiveUpdate {
  current: string | null = null;
  next: string | null = null;
  readonly downloaded = new Set<string>();
  readonly downloads: DownloadBundleOptions[] = [];
  readyCalls = 0;
  reloads = 0;
  /** Stands for the native check of the signature against the app's public key. */
  signatureHolds: (options: DownloadBundleOptions) => boolean = () => true;
  /** Awaited before each download completes, for a spec to observe it in progress. */
  downloadDelay: () => Promise<void> = async () => undefined;
  private previous: string | null = null;
  private rolledBack = false;
  private awaitingReady = false;

  readonly plugin: UpdatePlugins['liveUpdate'] = {
    ready: async () => this.ready(),
    getCurrentBundle: async () => ({ bundleId: this.current }),
    getNextBundle: async () => ({ bundleId: this.next }),
    getDownloadedBundles: async () => ({ bundleIds: [...this.downloaded] }),
    downloadBundle: async (options) => {
      await this.downloadDelay();
      this.download(options);
    },
    setNextBundle: async ({ bundleId }) => this.setNext(bundleId),
    deleteBundle: async ({ bundleId }) => {
      this.downloaded.delete(bundleId);
    },
    reload: async () => {
      this.reloads++;
      this.start();
    },
  };

  /** The app process starts: it runs the next bundle, and the rollback timer runs. */
  coldStart(): void {
    this.start();
  }

  /** The timer ends before any `ready()`: the plugin reloads on the embedded bundle. */
  expireReadyTimeout(): void {
    if (!this.awaitingReady) {
      return;
    }
    this.rolledBack = true;
    this.previous = this.current;
    this.current = null;
    this.next = null;
  }

  private start(): void {
    this.current = this.next;
    this.awaitingReady = true;
  }

  private ready() {
    this.readyCalls++;
    this.awaitingReady = false;
    const result = {
      currentBundleId: this.current,
      previousBundleId: this.previous,
      rollback: this.rolledBack,
    };
    this.previous = this.current;
    this.rolledBack = false;
    return result;
  }

  private download(options: DownloadBundleOptions): void {
    if (this.downloaded.has(options.bundleId)) {
      throw new Error('bundle already exists.');
    }
    this.downloads.push(options);
    if (!this.signatureHolds(options)) {
      throw new Error('Signature verification failed.');
    }
    this.downloaded.add(options.bundleId);
  }

  private setNext(bundleId: string | null): void {
    if (bundleId !== null && !this.downloaded.has(bundleId)) {
      throw new Error('bundle not found.');
    }
    this.next = bundleId;
  }
}
