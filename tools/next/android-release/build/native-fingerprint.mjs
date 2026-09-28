// Minimum native version of the bundles (lib/native-fingerprint.mjs), from the committed
// state of the repository (`git ls-files -s`: stage an edit before --update):
//
//   node tools/next/android-release/build/native-fingerprint.mjs --check X.Y.Z
//        prints minimum_native=A.B.C for the release X.Y.Z, or fails when the native layer
//        changed since native-baseline.json
//   node tools/next/android-release/build/native-fingerprint.mjs --update X.Y.Z
//        moves the minimum up to X.Y.Z, the release that changes the native layer
import { execFileSync } from 'node:child_process';
import { readFileSync, writeFileSync } from 'node:fs';
import { parseArgs } from 'node:util';
import {
  minimumNativeVersion,
  nativeFingerprint,
  nativePluginsOf,
} from '../lib/native-fingerprint.mjs';
import { versionCodeOf } from '../lib/release-version.mjs';

const ROOT = new URL('../../../../', import.meta.url);
const BASELINE_PATH = 'tools/next/android-release/native-baseline.json';
const BASELINE = new URL(BASELINE_PATH, ROOT);
const WEB = 'src/LoDb.Web';

function currentFingerprint() {
  const read = (path) => readFileSync(new URL(path, ROOT), 'utf8');
  const trackedFiles = execFileSync(
    'git',
    ['ls-files', '-s', '--', `${WEB}/android`, `${WEB}/capacitor.config.ts`],
    { cwd: ROOT, encoding: 'utf8' },
  );
  return nativeFingerprint({
    trackedFiles,
    plugins: nativePluginsOf(read(`${WEB}/android/capacitor.settings.gradle`)),
    lock: JSON.parse(read(`${WEB}/package-lock.json`)),
  });
}

try {
  const { values } = parseArgs({
    options: { check: { type: 'string' }, update: { type: 'string' } },
  });
  if (values.update) {
    versionCodeOf(values.update);
    const baseline = { minimumNativeVersion: values.update, fingerprint: currentFingerprint() };
    writeFileSync(BASELINE, `${JSON.stringify(baseline, null, 2)}\n`);
    console.error(`native-fingerprint: minimum ${values.update} written, commit ${BASELINE_PATH}`);
  } else if (values.check) {
    const minimum = minimumNativeVersion({
      baseline: JSON.parse(readFileSync(BASELINE, 'utf8')),
      fingerprint: currentFingerprint(),
      version: values.check,
    });
    process.stdout.write(`minimum_native=${minimum}\n`);
  } else {
    throw new Error('--check X.Y.Z or --update X.Y.Z is required');
  }
} catch (error) {
  console.error(`native-fingerprint: ${error.message}`);
  process.exitCode = 1;
}
