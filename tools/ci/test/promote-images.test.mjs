import assert from 'node:assert/strict';
import { readFileSync } from 'node:fs';
import { join } from 'node:path';
import { after, describe, it } from 'node:test';
import {
  IMAGES,
  PREFIX,
  ROOT,
  commit,
  digestOf,
  push,
  removeWorkspaces,
  runScript,
  tag,
  tagOf,
  untag,
  workspace,
} from './support/fake-registry.mjs';

// promote-images.sh against a registry kept in a folder and a real git history: prod gets the
// digests staging validated, built from the code main holds, or nothing moves.
const SCRIPT = 'tools/ci/promote-images.sh';
const CI_WORKFLOW = join(ROOT, '.github', 'workflows', 'ci.yml');
after(removeWorkspaces);

// Every image built from one revision, on staging and validated there.
function stageValidated(space, revision = space.revision) {
  IMAGES.forEach((image, index) => {
    const digest = digestOf(String(index + 1));
    push(space, image, { digest, revision });
    tag(space, image, 'staging', digest);
    tag(space, image, 'staging-validated', digest);
  });
}

const promote = (space) => runScript(space, SCRIPT, [], { IMAGE_PREFIX: PREFIX });
const prodTags = (space) => IMAGES.map((image) => tagOf(space, image, 'prod'));

describe('promote-images.sh', () => {
  it('tags the validated digests prod and latest when main holds their code', () => {
    const space = workspace();
    stageValidated(space);
    commit(space.checkout, 'docs/guide.md', 'documentation only\n');

    const result = promote(space);

    assert.equal(result.status, 0, result.stderr);
    assert.deepEqual(prodTags(space), [digestOf('1'), digestOf('2'), digestOf('3')]);
    assert.equal(tagOf(space, 'nginx', 'latest'), digestOf('3'));
    assert.match(result.stdout, new RegExp(`revision ${space.revision} promoted to prod latest`));
  });

  it('refuses when staging moved past what it validated', () => {
    const space = workspace();
    stageValidated(space);
    push(space, 'api', { digest: digestOf('9'), revision: space.revision });
    tag(space, 'api', 'staging', digestOf('9'));

    const result = promote(space);

    assert.equal(result.status, 1);
    assert.match(result.stderr, /api: staging .* is not what staging validated/);
    assert.deepEqual(prodTags(space), [null, null, null]);
  });

  it('refuses while staging has validated no build', () => {
    const space = workspace();
    stageValidated(space);
    untag(space, 'web-ssr', 'staging-validated');

    const result = promote(space);

    assert.equal(result.status, 1);
    assert.match(result.stderr, /web-ssr: no staging-validated image/);
    assert.deepEqual(prodTags(space), [null, null, null]);
  });

  it('refuses when main holds code that staging never served', () => {
    const space = workspace();
    stageValidated(space);
    commit(space.checkout, 'src/app.txt', 'changed after the build\n');

    const result = promote(space);

    assert.equal(result.status, 1);
    assert.match(result.stderr, /src\/app\.txt/);
    assert.match(result.stderr, /whose code differs from/);
    assert.deepEqual(prodTags(space), [null, null, null]);
  });

  it('refuses images of two builds', () => {
    const space = workspace();
    stageValidated(space);
    const other = commit(space.checkout, 'docs/other.md', 'another build\n');
    push(space, 'nginx', { digest: digestOf('8'), revision: other });
    tag(space, 'nginx', 'staging', digestOf('8'));
    tag(space, 'nginx', 'staging-validated', digestOf('8'));

    const result = promote(space);

    assert.equal(result.status, 1);
    assert.match(result.stderr, /staging mixes two builds: nginx is/);
    assert.deepEqual(prodTags(space), [null, null, null]);
  });

  it('refuses a revision the checkout does not have', () => {
    const space = workspace();
    stageValidated(space, 'f'.repeat(40));

    const result = promote(space);

    assert.equal(result.status, 1);
    assert.match(result.stderr, /a commit this checkout does not have/);
  });

  it('compares the paths ci.yml builds and deploys', () => {
    const workflow = readFileSync(CI_WORKFLOW, 'utf8');
    const block = /paths: &stack-paths\n((?:\s+- .+\n)+)/.exec(workflow);
    const script = readFileSync(join(ROOT, SCRIPT), 'utf8');
    const list = /readonly STACK_PATHS=\(\n([^)]*)\)/.exec(script);
    assert.ok(block && list, 'path lists not found');
    const fromWorkflow = block[1].split('\n').map((line) => line.replace(/^\s+- /, '').trim());
    const fromScript = list[1].trim().split(/\s+/).map((path) => path.replaceAll("'", ''));
    // A folder of the script is `folder/**` in the workflow; a file or a glob is as is.
    const asGlobs = fromScript.map((path) =>
      /[.*]/.test(path.split('/').pop()) ? path : `${path}/**`,
    );

    assert.deepEqual(asGlobs.sort(), fromWorkflow.filter(Boolean).sort());
  });
});
