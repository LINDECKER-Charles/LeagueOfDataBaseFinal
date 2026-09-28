/** Where the transitional channel reads its manifest, and where its APK may come from. */
export interface TransitionalSettings {
  readonly manifestUrl: string;
  /** Hosts (`host` of a URL) the APK may be downloaded from, over https only. */
  readonly apkHosts: readonly string[];
}
