import type { CapacitorConfig } from '@capacitor/cli';

/**
 * The Android app (ADR 0007): the store build of the shell, embedded in the APK and served
 * by the WebView from https://localhost, the origin the API's CORS policy lets in. No
 * `server.url`: the app starts without network and never loads its pages from the site.
 */
const config: CapacitorConfig = {
  appId: 'com.leagueofdatabase.app',
  appName: 'LeagueOfDataBase',
  webDir: 'dist/shell-store/browser',
  server: {
    androidScheme: 'https',
    hostname: 'localhost',
    // Empty: the WebView navigates to nothing but its bundle; external links go through
    // the system browser (@capacitor/browser).
    allowNavigation: [],
  },
  android: {
    // Every request of the bundle is https: the API and the images of the site.
    allowMixedContent: false,
  },
  plugins: {
    // Signed live update bundles (ADR 0008, docs/guides/release-android.md).
    LiveUpdate: {
      // The PEM public key of the bundle signer, read at `cap sync`: the plugin refuses a
      // bundle whose signature does not hold against it. Never the private key; without the
      // variable the release checks (check-embedded-config.mjs) stop the build.
      publicKey: process.env['LODB_LIVE_UPDATE_PUBLIC_KEY'],
      // A bundle that has not called `ready()` by then (AndroidUpdates, once the router
      // starts its first navigation) is rolled back to the one embedded in the APK.
      readyTimeout: 20_000,
    },
  },
};

export default config;
