// Checks the LiveUpdate settings an APK embeds (lib/embedded-config.mjs) against the public
// key of the bundle signer, before anything is signed or published:
//
//   LODB_LIVE_UPDATE_PUBLIC_KEY="$(cat lodb-live-update.pub.pem)" \
//   node tools/next/android-release/build/check-embedded-config.mjs --apk app-release.apk
//
// Prints ready_timeout=<ms>, for $GITHUB_OUTPUT: the gate waits for it.
import { execFileSync } from 'node:child_process';
import { parseArgs } from 'node:util';
import { EMBEDDED_CONFIG_ENTRY, liveUpdateSettingsOf } from '../lib/embedded-config.mjs';
import {
  PUBLIC_KEY_VARIABLE,
  keyFromEnvironment,
} from '../../live-update/lib/key-from-environment.mjs';

try {
  const { values } = parseArgs({ options: { apk: { type: 'string' } } });
  if (!values.apk) {
    throw new Error('--apk is required');
  }
  const json = execFileSync('unzip', ['-p', values.apk, EMBEDDED_CONFIG_ENTRY], {
    encoding: 'utf8',
  });
  const { readyTimeout } = liveUpdateSettingsOf(
    JSON.parse(json),
    keyFromEnvironment(PUBLIC_KEY_VARIABLE),
  );
  process.stdout.write(`ready_timeout=${readyTimeout}\n`);
} catch (error) {
  console.error(`check-embedded-config: ${error.message}`);
  process.exitCode = 1;
}
