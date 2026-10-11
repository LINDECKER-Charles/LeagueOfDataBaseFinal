import assert from 'node:assert/strict';
import { spawnSync } from 'node:child_process';
import { copyFileSync, mkdirSync, readFileSync, writeFileSync } from 'node:fs';
import { delimiter, join } from 'node:path';
import { after, describe, it } from 'node:test';
import {
  IMAGES,
  ROOT,
  digestOf,
  push,
  removeWorkspaces,
  tag,
  tagOf,
  workspace,
} from './support/fake-registry.mjs';

// The promotion step of _promote.yml, run as GitHub runs a step that names no shell
// (`bash -e`, without pipefail), against a registry kept in a folder: the step must fail when
// the script refuses, or deploy-prod would deploy main with images staging never validated.
const WORKFLOW = join(ROOT, '.github', 'workflows', '_promote.yml');
const SCRIPT_CALL = 'bash tools/ci/promote-images.sh';
const SCRIPTS = ['promote-images.sh', 'tag-image.sh'];
const OWNER = 'Owner';
after(removeWorkspaces);

// The `run: |` block of the step that calls the script, without its indentation.
function promoteStep() {
  const lines = readFileSync(WORKFLOW, 'utf8').split(/\r?\n/);
  const call = lines.findIndex((line) => line.includes(SCRIPT_CALL));
  const run = lines.findLastIndex((line, index) => index < call && line.trim() === 'run: |');
  assert.ok(call > 0 && run > 0, 'promotion step not found');
  const indent = lines[run + 1].search(/\S/);
  const body = lines.slice(run + 1);
  const end = body.findIndex((line) => line.trim() !== '' && line.search(/\S/) < indent);
  return body.slice(0, end === -1 ? undefined : end).map((line) => line.slice(indent)).join('\n');
}

// A checkout holding the scripts, the registry stub first on the PATH.
function runner() {
  const space = workspace();
  mkdirSync(join(space.checkout, 'tools', 'ci'), { recursive: true });
  for (const script of SCRIPTS) {
    copyFileSync(join(ROOT, 'tools', 'ci', script), join(space.checkout, 'tools', 'ci', script));
  }
  const summary = join(space.dir, 'summary.md');
  writeFileSync(summary, '');
  return { ...space, summary };
}

function runStep(space) {
  return spawnSync('bash', ['-e', '-s'], {
    input: `${promoteStep()}\n`,
    cwd: space.checkout,
    encoding: 'utf8',
    env: {
      ...process.env,
      PATH: `${join(space.dir, 'bin')}${delimiter}${process.env.PATH}`,
      FAKE_REGISTRY: join(space.dir, 'registry'),
      GITHUB_REPOSITORY_OWNER: OWNER,
      RUNNER_TEMP: space.dir,
      GITHUB_STEP_SUMMARY: space.summary,
      SOURCE_TAG: 'staging',
      VALIDATED_TAG: 'staging-validated',
      TARGET_TAGS: 'prod latest',
    },
  });
}

function stage(space, { isValidated }) {
  IMAGES.forEach((image, index) => {
    const digest = digestOf(String(index + 1));
    push(space, image, { digest, revision: space.revision });
    tag(space, image, 'staging', digest);
    if (isValidated) {
      tag(space, image, 'staging-validated', digest);
    }
  });
}

describe('_promote.yml, the promotion step', () => {
  it('fails when the script refuses, and writes why to the summary', () => {
    const space = runner();
    stage(space, { isValidated: false });

    const result = runStep(space);

    assert.notEqual(result.status, 0, result.stdout);
    assert.match(readFileSync(space.summary, 'utf8'), /no staging-validated image/);
    assert.equal(tagOf(space, 'api', 'prod'), null);
  });

  it('passes once the script promoted the validated digests', () => {
    const space = runner();
    stage(space, { isValidated: true });

    const result = runStep(space);

    assert.equal(result.status, 0, result.stderr);
    assert.equal(tagOf(space, 'nginx', 'prod'), digestOf('3'));
    assert.match(readFileSync(space.summary, 'utf8'), /promoted to prod latest/);
  });
});
