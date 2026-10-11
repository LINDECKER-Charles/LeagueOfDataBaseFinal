import assert from 'node:assert/strict';
import { spawnSync } from 'node:child_process';
import {
  chmodSync,
  copyFileSync,
  mkdirSync,
  mkdtempSync,
  readFileSync,
  rmSync,
  writeFileSync,
} from 'node:fs';
import { tmpdir } from 'node:os';
import { basename, delimiter, join, posix } from 'node:path';
import { after, describe, it } from 'node:test';
import { fileURLToPath } from 'node:url';

// The scan step of _build.yml, run as GitHub runs a step that names no shell (`bash -e`), with
// a `docker` stub that records what the scanner would see. Trivy reads its configuration
// (trivy.yaml) and its ignore file from its working folder: the checkout must not be that
// folder, or a committed file could narrow what the scan reports.
const ROOT = fileURLToPath(new URL('../../..', import.meta.url));
const WORKFLOW = join(ROOT, '.github', 'workflows', '_build.yml');
const SCANNER_CALL = '"$TRIVY_IMAGE" image';
const DOCKER_STUB = join(ROOT, 'tools', 'ci', 'test', 'fixtures', 'docker-scan.sh');
const IGNORE_FILE = '.trivyignore';
const CONFIG_FILE = 'trivy.yaml';
const workspaces = [];
after(() => workspaces.splice(0).forEach((dir) => rmSync(dir, { recursive: true, force: true })));

// The `run: |` block of the step that starts the scanner, without its indentation.
function scanStep() {
  const lines = readFileSync(WORKFLOW, 'utf8').split(/\r?\n/);
  const call = lines.findIndex((line) => line.includes(SCANNER_CALL));
  const run = lines.findLastIndex((line, index) => index < call && line.trim() === 'run: |');
  assert.ok(call > 0 && run > 0, 'scan step not found');
  const indent = lines[run + 1].search(/\S/);
  const body = lines.slice(run + 1);
  const end = body.findIndex((line) => line.trim() !== '' && line.search(/\S/) < indent);
  return body.slice(0, end === -1 ? undefined : end).map((line) => line.slice(indent)).join('\n');
}

/** A checkout holding `files` ({ path: text }), a runner folder and the stub. */
function runner(files) {
  const dir = mkdtempSync(join(tmpdir(), 'lodb-scan-'));
  workspaces.push(dir);
  const checkout = join(dir, 'checkout');
  for (const [path, text] of Object.entries(files)) {
    mkdirSync(join(checkout, path, '..'), { recursive: true });
    writeFileSync(join(checkout, path), text);
  }
  mkdirSync(join(dir, 'bin'));
  copyFileSync(DOCKER_STUB, join(dir, 'bin', 'docker'));
  chmodSync(join(dir, 'bin', 'docker'), 0o755);
  mkdirSync(join(dir, 'temp'));
  return { dir, checkout };
}

function runStep(space, scanStatus = 0) {
  const record = join(space.dir, 'record.txt');
  const result = spawnSync('bash', ['-e', '-s'], {
    input: `${scanStep()}\n`,
    cwd: space.checkout,
    encoding: 'utf8',
    env: {
      ...process.env,
      PATH: `${join(space.dir, 'bin')}${delimiter}${process.env.PATH}`,
      RUNNER_TEMP: join(space.dir, 'temp'),
      IMAGE: 'ghcr.io/owner/lodb/api',
      DIGEST: `sha256:${'a'.repeat(64)}`,
      TRIVY_IMAGE: 'trivy:pinned',
      FAKE_SCAN_RECORD: record,
      FAKE_SCAN_STATUS: String(scanStatus),
    },
  });
  return { result, record };
}

// What the scanner saw: its working folder, the files its mounts show, the folder on the
// runner mounted on the working folder, and its arguments.
function seenBy(record) {
  const lines = readFileSync(record, 'utf8').split(/\r?\n/);
  const valueOf = (prefix) => lines.find((line) => line.startsWith(prefix))?.slice(prefix.length);
  const workdir = valueOf('workdir ');
  const mounts = lines.filter((line) => line.startsWith('mount ')).map((line) => {
    const spec = line.slice('mount '.length);
    const space = spec.lastIndexOf(' ');
    return { source: spec.slice(0, space), target: spec.slice(space + 1) };
  });
  const files = lines.filter((line) => line.startsWith('file ')).map((line) => line.slice(5));
  const workFolder = mounts.find((mount) => mount.target === workdir)?.source;
  return { workdir, files, workFolder, args: valueOf('args ') ?? '' };
}

const filesIn = (seen, folder) =>
  seen.files.filter((file) => posix.dirname(file) === folder).map((file) => basename(file));

describe('_build.yml, the scan step', () => {
  it('runs the scanner in a folder that holds the archive only, never the checkout', () => {
    const space = runner({ [CONFIG_FILE]: 'pkg:\n  types: [library]\n', 'src/app.txt': 'x\n' });

    const { result, record } = runStep(space);

    assert.equal(result.status, 0, result.stderr);
    const seen = seenBy(record);
    assert.deepEqual(filesIn(seen, seen.workdir), ['image.tar']);
    assert.ok(!seen.files.some((file) => basename(file) === CONFIG_FILE), seen.files.join('\n'));
    assert.match(seen.args, new RegExp(`--input ${seen.workdir}/image.tar `));
    assert.match(seen.args, /--severity HIGH,CRITICAL --ignore-unfixed --exit-code 1 /);
  });

  it('hands the scanner the .trivyignore of the checkout, in its working folder', () => {
    const accepted = '# reason\nCVE-2026-1 exp:2026-11-01\n';
    const space = runner({ [IGNORE_FILE]: accepted, [CONFIG_FILE]: 'severity: [LOW]\n' });

    const { result, record } = runStep(space);

    assert.equal(result.status, 0, result.stderr);
    const seen = seenBy(record);
    assert.deepEqual(filesIn(seen, seen.workdir).sort(), [IGNORE_FILE, 'image.tar']);
    assert.equal(readFileSync(join(seen.workFolder, IGNORE_FILE), 'utf8'), accepted);
  });

  it('fails when the scanner reports a blocking vulnerability', () => {
    const space = runner({ 'src/app.txt': 'x\n' });

    const { result } = runStep(space, 1);

    assert.notEqual(result.status, 0);
  });
});
