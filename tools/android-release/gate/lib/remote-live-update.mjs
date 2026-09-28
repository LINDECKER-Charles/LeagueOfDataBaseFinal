// The LiveUpdate plugin of the running app, called through the Capacitor bridge the WebView
// injects into every page it serves: it answers even on a bundle whose app never starts.
import { evaluateInApp } from './app-page.mjs';

const PLUGIN = 'LiveUpdate';
const CALL_TIMEOUT_MS = 15_000;
// A bundle is the whole shell build: a few megabytes over the runner's network.
const DOWNLOAD_TIMEOUT_MS = 180_000;

function call(method, options = {}, timeoutMs = CALL_TIMEOUT_MS) {
  const args = [PLUGIN, method, options].map((value) => JSON.stringify(value)).join(', ');
  return evaluateInApp(`Capacitor.nativePromise(${args})`, timeoutMs);
}

/** The id of the running bundle; null for the one embedded in the APK. */
export async function currentBundle() {
  return (await call('getCurrentBundle')).bundleId ?? null;
}

export async function downloadedBundles() {
  return (await call('getDownloadedBundles')).bundleIds;
}

/** Downloads a bundle of its descriptor: the plugin checks its signature first. */
export async function download({ id, url, checksum, signature }) {
  await call('downloadBundle', { bundleId: id, url, checksum, signature }, DOWNLOAD_TIMEOUT_MS);
}

export async function setNextBundle(bundleId) {
  await call('setNextBundle', { bundleId });
}

/** Whether Angular rendered the app: only a bundle that starts gets there. */
export async function appRendered() {
  return evaluateInApp(
    "(document.querySelector('lodb-root')?.childElementCount ?? 0) > 0",
    CALL_TIMEOUT_MS,
  );
}
