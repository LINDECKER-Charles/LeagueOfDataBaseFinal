import type { LiveUpdateBundle } from '../../../../api/generated/models/live-update-bundle';

/**
 * What the live update must do so that the next cold start runs the bundle the policy names
 * (ADR 0008, level 1):
 * - `keep`: the next start already runs it;
 * - `download`: fetch it (the plugin checks its signature), then start on it next time;
 * - `activate`: it is already on the device, start on it next time;
 * - `reset`: start on the bundle embedded in the APK next time.
 */
export type LiveStep =
  | { readonly kind: 'keep' }
  | { readonly kind: 'download'; readonly bundle: LiveUpdateBundle }
  | { readonly kind: 'activate'; readonly bundleId: string }
  | { readonly kind: 'reset' };
