// The emulator of the gate, through adb (platform-tools on the PATH).
import { execFileSync } from 'node:child_process';

export const APP_PACKAGE = 'com.leagueofdatabase.app';
const MAIN_ACTIVITY = `${APP_PACKAGE}/.MainActivity`;

/** Runs adb and returns its output; throws with adb's message on failure. */
export function adb(...args) {
  return execFileSync('adb', args, { encoding: 'utf8', stdio: ['ignore', 'pipe', 'pipe'] });
}

export function install(apk) {
  adb('install', '-r', apk);
}

/** Uninstalls the app when it is there: the release and the debug APK differ in signature. */
export function uninstall() {
  try {
    adb('uninstall', APP_PACKAGE);
  } catch {
    // Not installed.
  }
}

/** A cold start: the process dies first, so that the plugin applies the next bundle. */
export function coldStart() {
  adb('shell', 'am', 'force-stop', APP_PACKAGE);
  const output = adb('shell', 'am', 'start', '-W', '-n', MAIN_ACTIVITY);
  if (!/Status: ok/.test(output)) {
    throw new Error(`the app did not start:\n${output}`);
  }
}

/** The pid of the app's process; null while it does not run. */
export function pidOf() {
  try {
    return adb('shell', 'pidof', APP_PACKAGE).trim() || null;
  } catch {
    return null;
  }
}

/**
 * Cuts the emulator from the network, or plugs it back: offline, the app's own checks
 * cannot read the client policy of the real API, so that only the gate moves bundles.
 */
export function setOnline(online) {
  const state = online ? 'enable' : 'disable';
  adb('shell', 'svc', 'wifi', state);
  adb('shell', 'svc', 'data', state);
  adb('shell', 'cmd', 'connectivity', 'airplane-mode', online ? 'disable' : 'enable');
}
