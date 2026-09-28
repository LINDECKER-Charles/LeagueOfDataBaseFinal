// Version of the Android release of a commit, from its tags (lib/release-version.mjs):
//
//   node tools/android-release/build/release-version.mjs --sha <40 hex>
//
// Prints, for $GITHUB_OUTPUT: release=true|false, then version=, version_code= and tag=, or
// reason= when there is nothing to release. Several android-vX.Y.Z tags on one commit fail.
import { execFileSync } from 'node:child_process';
import { parseArgs } from 'node:util';
import { TAG_PREFIX, releaseOf } from '../lib/release-version.mjs';

const SHA = /^[0-9a-f]{40}$/;

try {
  const { values } = parseArgs({ options: { sha: { type: 'string' } } });
  if (!SHA.test(values.sha ?? '')) {
    throw new Error(`--sha must be a full commit SHA, got '${values.sha ?? ''}'`);
  }
  const tags = execFileSync('git', ['tag', '--points-at', values.sha, '--list', `${TAG_PREFIX}*`], {
    encoding: 'utf8',
  });
  const release = releaseOf(tags.split('\n').filter(Boolean));
  const lines = release.release
    ? [
        'release=true',
        `version=${release.version}`,
        `version_code=${release.versionCode}`,
        `tag=${release.tag}`,
      ]
    : ['release=false', `reason=${release.reason} on ${values.sha}`];
  process.stdout.write(`${lines.join('\n')}\n`);
} catch (error) {
  console.error(`release-version: ${error.message}`);
  process.exitCode = 1;
}
