/** Settings that differ between the `web` and `shell` builds (fileReplacements). */
export interface AppEnvironment {
  /**
   * Public origin of the API (scheme, host, port; no path), or null when the API shares the
   * page origin. Only the Android shell needs it: its WebView serves the pages from
   * https://localhost, while the desktop host proxies the API on its own loopback origin.
   */
  readonly publicApiOrigin: string | null;
}
