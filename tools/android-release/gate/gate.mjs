// The update gate of an Android release (L10.4, ADR 0008), on a booted emulator; nothing is
// published unless it passes:
//
//   node tools/android-release/gate/gate.mjs --release-apk lodb-X.Y.Z.apk \
//        --debug-apk app-debug.apk --bundles bundles --version X.Y.Z --ready-timeout <ms>
//
// 1. The signed release APK installs and starts.
// 2. The debug APK starts on its embedded bundle. It embeds the same front and LiveUpdate
//    settings as the release, and opens its WebView to DevTools, through which the gate
//    drives the plugin (lib/remote-live-update.mjs).
// 3. A tampered bundle is refused: its signature does not hold.
// 4. A faulty bundle, signed but never starting, runs at the next cold start, then the
//    plugin rolls it back to the embedded bundle after the ready timeout, and the app
//    removes it.
// 5. The bundle of the release runs at the next cold start, and still runs after the ready
//    timeout: the app confirmed its start.
// The emulator stays offline but while the gate downloads, so that the app's own checks
// read no client policy and move no bundle behind the gate's back.
import { readFileSync } from 'node:fs';
import { join } from 'node:path';
import { parseArgs } from 'node:util';
import { coldStart, install, pidOf, setOnline, uninstall } from './lib/adb.mjs';
import {
  appRendered,
  currentBundle,
  download,
  downloadedBundles,
  setNextBundle,
} from './lib/remote-live-update.mjs';
import { poll, sleep } from './lib/wait.mjs';

// Beyond the ready timeout: the rollback reloads the WebView, which takes a few seconds.
const ROLLBACK_MARGIN_MS = 15_000;
const START_TIMEOUT_MS = 60_000;
const RELEASE_ALIVE_MS = 10_000;

function readOptions() {
  const names = ['release-apk', 'debug-apk', 'bundles', 'version', 'ready-timeout'];
  const { values } = parseArgs({
    options: Object.fromEntries(names.map((name) => [name, { type: 'string' }])),
  });
  const missing = names.filter((name) => !values[name]);
  const readyTimeout = Number(values['ready-timeout']);
  if (missing.length > 0 || !Number.isSafeInteger(readyTimeout) || readyTimeout <= 0) {
    throw new Error(`needs --${names.join(' --')} (a ready timeout in ms)`);
  }
  const bundle = (name) => JSON.parse(readFileSync(join(values.bundles, name), 'utf8'));
  return {
    ...values,
    readyTimeout,
    release: bundle(`lodb-bundle-${values.version}.json`),
    faulty: bundle(`gate/lodb-bundle-${values.version}-gate-faulty.json`),
    tampered: bundle(`gate/lodb-bundle-${values.version}-gate-tampered.json`),
  };
}

async function step(title, run) {
  console.log(`gate: ${title}`);
  await run();
  console.log('gate:   ok');
}

async function releaseApkStarts(apk) {
  uninstall();
  install(apk);
  coldStart();
  await sleep(RELEASE_ALIVE_MS);
  if (pidOf() === null) {
    throw new Error('the release APK stopped after its start');
  }
  uninstall();
}

async function startsOnEmbedded(apk) {
  install(apk);
  coldStart();
  await poll(appRendered, { timeoutMs: START_TIMEOUT_MS, what: 'the app to render' });
  const current = await currentBundle();
  if (current !== null) {
    throw new Error(`a fresh install runs the bundle ${current}, not the embedded one`);
  }
}

async function refusesTampered(tampered) {
  const failure = await download(tampered).then(
    () => null,
    (error) => error,
  );
  if (failure === null || !/signature/i.test(failure.message)) {
    throw new Error(`the tampered bundle was not refused: ${failure?.message ?? 'downloaded'}`);
  }
}

async function rollsBackFaulty(faulty, readyTimeout) {
  await setNextBundle(faulty.id);
  coldStart();
  await poll(async () => (await currentBundle()) === faulty.id, {
    timeoutMs: readyTimeout,
    what: `the faulty bundle ${faulty.id} to run`,
  });
  await sleep(readyTimeout);
  await poll(async () => (await currentBundle()) === null && (await appRendered()), {
    timeoutMs: ROLLBACK_MARGIN_MS,
    what: 'the rollback to the embedded bundle',
  });
  await poll(async () => !(await downloadedBundles()).includes(faulty.id), {
    timeoutMs: START_TIMEOUT_MS,
    what: `the app to remove the faulty bundle ${faulty.id}`,
  });
}

async function keepsRelease(release, readyTimeout) {
  await setNextBundle(release.id);
  coldStart();
  await poll(async () => (await currentBundle()) === release.id && (await appRendered()), {
    timeoutMs: START_TIMEOUT_MS,
    what: `the bundle ${release.id} to run`,
  });
  await sleep(readyTimeout + ROLLBACK_MARGIN_MS);
  const current = await currentBundle();
  if (current !== release.id) {
    throw new Error(`the bundle ${release.id} was rolled back to ${current ?? 'the embedded one'}`);
  }
}

async function run(options) {
  const { readyTimeout, release, faulty, tampered } = options;
  await step('the signed release APK installs and starts', () =>
    releaseApkStarts(options['release-apk']),
  );
  await step('the debug APK starts on its embedded bundle', () =>
    startsOnEmbedded(options['debug-apk']),
  );
  setOnline(true);
  await step('a tampered bundle is refused', () => refusesTampered(tampered));
  await step('the faulty and the release bundles download', async () => {
    await download(faulty);
    await download(release);
  });
  setOnline(false);
  await step('a faulty bundle is rolled back', () => rollsBackFaulty(faulty, readyTimeout));
  await step('the release bundle starts and stays', () => keepsRelease(release, readyTimeout));
}

try {
  const options = readOptions();
  setOnline(false);
  try {
    await run(options);
  } finally {
    setOnline(true);
  }
  console.log('gate: passed');
} catch (error) {
  console.error(`gate: FAILED: ${error.message}`);
  process.exitCode = 1;
}
