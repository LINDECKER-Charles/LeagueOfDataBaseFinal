/** Settings that differ between the `web`, `shell` and `shell-store` builds (fileReplacements). */
export interface AppEnvironment {
  /**
   * Public origin of the API (scheme, host, port; no path), or null when the API shares the
   * page origin. Only the Android shell needs it: its WebView serves the pages from
   * https://localhost, while the desktop host proxies the API on its own loopback origin.
   */
  readonly publicApiOrigin: string | null;

  /**
   * Whether the build may show a payment: donations and API credit checkouts. False in the
   * store build of the apps (ADR 0007): Play only sells digital goods through its own
   * billing. The API keeps its payment endpoints for the web. The app reads it through the
   * PAYMENTS_ENABLED token (payments-enabled.ts).
   */
  readonly payments: boolean;
}
