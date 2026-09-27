import { Injectable, type Signal, inject, signal } from '@angular/core';
import type { UpdateState } from '../../../update-state';
import type { LiveStep } from '../plan/live-step';
import { UPDATE_PLUGINS } from '../native/update-plugins-token';
import type { LiveFacts } from './live-facts';
import { RejectedBundles } from './rejected-bundles';

/**
 * Level 1 of ADR 0008: the front updates itself with signed bundles, which the plugin checks
 * against the public key of the app before unpacking. A new bundle only runs from the next
 * cold start, or when the user restarts on it: never in the middle of a session.
 */
@Injectable({ providedIn: 'root' })
export class LiveUpdates {
  private readonly plugin = inject(UPDATE_PLUGINS).liveUpdate;
  private readonly rejected = inject(RejectedBundles);
  private readonly current = signal<UpdateState>('none');

  /** `ready` while the next start runs another bundle than the running one. */
  readonly state: Signal<UpdateState> = this.current.asReadonly();

  /**
   * Tells the plugin this bundle started, which stops its rollback timer: a bundle that never
   * gets here is replaced by the embedded one when the timer ends. The bundle rolled back
   * before this start is remembered and removed. Must precede any change of bundle.
   */
  async confirmStart(): Promise<void> {
    const { rollback, previousBundleId } = await this.plugin.ready();
    if (rollback && previousBundleId !== null) {
      await this.rejected.add(previousBundleId);
      await this.plugin.deleteBundle({ bundleId: previousBundleId }).catch(() => undefined);
    }
    await this.refresh();
  }

  async facts(): Promise<LiveFacts> {
    const [next, downloaded, rejected] = await Promise.all([
      this.plugin.getNextBundle(),
      this.plugin.getDownloadedBundles(),
      this.rejected.list(),
    ]);
    return {
      nextBundleId: next.bundleId,
      downloadedBundleIds: downloaded.bundleIds,
      rejectedBundleIds: rejected,
    };
  }

  /** Prepares the next start. A failure changes nothing: the next check tries again. */
  async run(step: LiveStep): Promise<void> {
    try {
      await this.prepare(step);
    } catch {
      // Download, signature or storage failure: the device keeps the bundles it had.
    }
    await this.refresh();
  }

  /** Restarts the page on the next bundle now, when the user asks for it. */
  async apply(): Promise<void> {
    await this.plugin.reload();
  }

  private async prepare(step: LiveStep): Promise<void> {
    switch (step.kind) {
      case 'download': {
        const { id, url, checksum, signature } = step.bundle;
        this.current.set('downloading');
        await this.plugin.downloadBundle({ bundleId: id, url, checksum, signature });
        await this.plugin.setNextBundle({ bundleId: id });
        return;
      }
      case 'activate':
        await this.plugin.setNextBundle({ bundleId: step.bundleId });
        return;
      case 'reset':
        await this.plugin.setNextBundle({ bundleId: null });
        return;
    }
  }

  private async refresh(): Promise<void> {
    const bundles = await Promise.all([
      this.plugin.getCurrentBundle(),
      this.plugin.getNextBundle(),
    ]).catch(() => null);
    const pending = bundles !== null && bundles[0].bundleId !== bundles[1].bundleId;
    this.current.set(pending ? 'ready' : 'none');
  }
}
