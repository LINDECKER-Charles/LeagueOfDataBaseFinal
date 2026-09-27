import type { PlatformPolicy } from '../../../../api/generated/models/platform-policy';

/** Everything the update decision reads, gathered at each check. */
export interface UpdateFacts {
  /** `versionName` of the installed shell, the one `X-LoDb-Client` states. */
  readonly nativeVersion: string;
  /** Android's entry of `GET /api/client-policy`, as the API sent it. */
  readonly policy: PlatformPolicy;
  /** The bundle the next cold start runs; null for the one embedded in the APK. */
  readonly nextBundleId: string | null;
  /** The bundles already on the device. */
  readonly downloadedBundleIds: readonly string[];
  /** The bundles that failed to start on this device and were rolled back. */
  readonly rejectedBundleIds: readonly string[];
}
