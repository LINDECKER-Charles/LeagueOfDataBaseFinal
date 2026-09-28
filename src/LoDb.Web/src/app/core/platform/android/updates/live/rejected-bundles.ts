import { Injectable, inject } from '@angular/core';
import { ANDROID_PLUGINS } from '../../native/android-plugins-token';
import { SecureItem } from '../../native/secure-item';

const STORAGE_KEY = 'lodb.liveUpdate.rejected';
// Enough for every bundle of a long outage; the oldest ids are long out of the policy.
const KEPT = 20;

function idsOf(json: string | null): string[] {
  try {
    const value: unknown = JSON.parse(json ?? '[]');
    return Array.isArray(value) ? value.filter((id) => typeof id === 'string') : [];
  } catch {
    return [];
  }
}

/**
 * The bundles that failed to start on this device (the plugin rolled them back), kept across
 * starts so that one the policy still names is never downloaded again: withdrawing it is the
 * API's job. Held by the app rather than by the plugin's own block list, which depends on a
 * native option.
 */
@Injectable({ providedIn: 'root' })
export class RejectedBundles {
  private readonly item = new SecureItem(inject(ANDROID_PLUGINS).secureStorage, STORAGE_KEY);

  async list(): Promise<string[]> {
    return idsOf(await this.item.read());
  }

  async add(bundleId: string): Promise<void> {
    const ids = (await this.list()).filter((id) => id !== bundleId);
    await this.item.write(JSON.stringify([...ids, bundleId].slice(-KEPT)));
  }
}
