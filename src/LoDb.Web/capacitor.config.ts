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
};

export default config;
