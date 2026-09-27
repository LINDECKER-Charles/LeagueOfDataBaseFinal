// The configuration Capacitor embeds in the APK (assets/capacitor.config.json, written by
// `cap sync` from capacitor.config.ts). The live update is only safe with two settings of
// the LiveUpdate plugin: the public key, without which any bundle would be taken, and the
// ready timeout, without which a faulty bundle would never be rolled back.

export const EMBEDDED_CONFIG_ENTRY = 'assets/capacitor.config.json';
const PEM_FRAME = /-----(BEGIN|END) PUBLIC KEY-----|\s/g;

function keyBodyOf(pem) {
  return String(pem).replace(PEM_FRAME, '');
}

/**
 * Checks the embedded configuration against the release's public key; returns the ready
 * timeout in milliseconds, which the gate waits for.
 */
export function liveUpdateSettingsOf(config, publicKeyPem) {
  if (JSON.stringify(config).includes('PRIVATE KEY')) {
    throw new Error('the app embeds a private key: never publish this build');
  }
  const liveUpdate = config?.plugins?.LiveUpdate;
  if (typeof liveUpdate?.publicKey !== 'string') {
    throw new Error('no plugins.LiveUpdate.publicKey: the app would take unsigned bundles');
  }
  if (keyBodyOf(liveUpdate.publicKey) !== keyBodyOf(publicKeyPem)) {
    throw new Error('plugins.LiveUpdate.publicKey is not the public key of the bundle signer');
  }
  const { readyTimeout } = liveUpdate;
  if (!Number.isSafeInteger(readyTimeout) || readyTimeout <= 0) {
    throw new Error('plugins.LiveUpdate.readyTimeout is not set: no faulty bundle rolls back');
  }
  return { readyTimeout };
}
