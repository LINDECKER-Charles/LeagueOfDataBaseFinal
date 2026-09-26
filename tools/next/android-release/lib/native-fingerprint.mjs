// The minimum native version of a live update bundle (ADR 0008): the oldest shell whose
// native layer the bundle's front works with. A front calling a plugin an older shell lacks
// would break there, so the minimum must move up to the release that changed the layer.
//
// native-baseline.json keeps that minimum with a fingerprint of the native layer: the
// tracked files of the Android project, capacitor.config.ts, and the version of every
// native plugin that capacitor.settings.gradle includes. A release whose layer differs from
// the baseline stops, until the baseline moves the minimum up to that release.
import { createHash } from 'node:crypto';
import { versionCodeOf } from './release-version.mjs';

// `new File('../node_modules/@scope/name/android')` in capacitor.settings.gradle.
const PLUGIN_PATH = /node_modules\/((?:@[^/'"]+\/)?[^/'"]+)\//g;

/** The npm packages of the native plugins, as `cap sync` wrote them into Gradle. */
export function nativePluginsOf(settingsGradle) {
  const names = [...settingsGradle.matchAll(PLUGIN_PATH)].map((match) => match[1]);
  return [...new Set(names)].sort();
}

/**
 * SHA-256 of the native layer. `trackedFiles` is the output of `git ls-files -s` (mode and
 * blob of each file, so any edit shows); `lock` is package-lock.json, for the plugin versions.
 */
export function nativeFingerprint({ trackedFiles, plugins, lock }) {
  const versions = plugins.map((name) => {
    const version = lock.packages?.[`node_modules/${name}`]?.version;
    if (!version) {
      throw new Error(`package-lock.json has no version of the native plugin ${name}`);
    }
    return `${name}@${version}`;
  });
  const files = trackedFiles
    .split('\n')
    .map((line) => line.trim())
    .filter(Boolean)
    .sort();
  return createHash('sha256')
    .update([...files, ...versions].join('\n'))
    .digest('hex');
}

/** The minimum native version for a bundle of `version`; throws when the baseline is stale. */
export function minimumNativeVersion({ baseline, fingerprint, version }) {
  const minimum = baseline?.minimumNativeVersion;
  if (baseline?.fingerprint !== fingerprint) {
    throw new Error(
      `the native layer changed since the baseline (minimum ${minimum}): older shells may ` +
        `lack what this front calls. Run native-fingerprint.mjs --update ${version} and commit.`,
    );
  }
  if (versionCodeOf(minimum) > versionCodeOf(version)) {
    throw new Error(`the baseline minimum ${minimum} is above the release ${version}`);
  }
  return minimum;
}
