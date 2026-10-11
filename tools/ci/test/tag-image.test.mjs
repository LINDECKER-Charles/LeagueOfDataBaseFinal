import assert from 'node:assert/strict';
import { after, describe, it } from 'node:test';
import {
  PREFIX,
  digestOf,
  push,
  removeWorkspaces,
  runScript,
  tagOf,
  workspace,
} from './support/fake-registry.mjs';

// tag-image.sh against a registry kept in a folder: tags are created from a digest, and the
// script proves afterwards that each one names it.
const SCRIPT = 'tools/ci/tag-image.sh';
after(removeWorkspaces);

function pushed() {
  const space = workspace();
  push(space, 'api', { digest: digestOf('1'), revision: space.revision });
  return space;
}

describe('tag-image.sh', () => {
  it('points every tag at the digest', () => {
    const space = pushed();

    const result = runScript(space, SCRIPT, [`${PREFIX}/api`, digestOf('1'), 'prod', 'latest']);

    assert.equal(result.status, 0, result.stderr);
    assert.equal(tagOf(space, 'api', 'prod'), digestOf('1'));
    assert.equal(tagOf(space, 'api', 'latest'), digestOf('1'));
  });

  it('refuses what is not a digest, before any write', () => {
    const space = pushed();

    const result = runScript(space, SCRIPT, [`${PREFIX}/api`, 'staging', 'prod']);

    assert.equal(result.status, 1);
    assert.match(result.stderr, /'staging' is not a sha256 digest/);
    assert.equal(tagOf(space, 'api', 'prod'), null);
  });

  it('refuses what is not a tag', () => {
    const space = pushed();

    const result = runScript(space, SCRIPT, [`${PREFIX}/api`, digestOf('1'), '-prod']);

    assert.equal(result.status, 1);
    assert.match(result.stderr, /'-prod' is not a tag/);
  });

  it('fails when a tag does not name the digest once created', () => {
    const space = pushed();

    const result = runScript(space, SCRIPT, [`${PREFIX}/api`, digestOf('1'), 'prod'], {
      FAKE_CREATE_DIGEST: digestOf('7'),
    });

    assert.equal(result.status, 1);
    assert.match(result.stderr, /api:prod names sha256:7+, not sha256:1+/);
  });

  it('fails when the registry holds no such digest', () => {
    const space = pushed();

    const result = runScript(space, SCRIPT, [`${PREFIX}/api`, digestOf('5'), 'prod']);

    assert.notEqual(result.status, 0);
    assert.equal(tagOf(space, 'api', 'prod'), null);
  });
});
