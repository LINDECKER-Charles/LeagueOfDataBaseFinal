/**
 * `latest.json` of the transitional channel (ADR 0008), written by the release workflow
 * next to the APK it describes (tools/next/android-release/latest-manifest.mjs).
 */
export interface ApkManifest {
  /** `versionCode` of the APK: the channel offers it when it is above the installed one. */
  readonly versionCode: number;
  readonly versionName: string;
  /** Where the system browser downloads the APK. */
  readonly url: string;
  /** SHA-256 of the APK, checked by the workflow once the asset is published. */
  readonly sha256: string;
}
