import assert from 'node:assert/strict';
import { mkdtempSync, readFileSync, rmSync, writeFileSync } from 'node:fs';
import { tmpdir } from 'node:os';
import { join } from 'node:path';
import { after, before, describe, it } from 'node:test';
import { runConversion } from '../../lib/convert/run-conversion.mjs';
import { renderLikeTransloco } from '../support/render-like-transloco.mjs';

const FIXTURES = new URL('../fixtures/', import.meta.url).pathname;

describe('runConversion', () => {
  let catalogueDir;
  let written;
  const read = (...path) => JSON.parse(readFileSync(join(catalogueDir, ...path), 'utf8'));

  before(() => {
    catalogueDir = mkdtempSync(join(tmpdir(), 'lodb-i18n-'));
    writeFileSync(join(catalogueDir, 'kept.json'), '{}');
    written = runConversion({ yamlDir: FIXTURES, catalogueDir });
  });

  after(() => rmSync(catalogueDir, { recursive: true, force: true }));

  it('writes messages at the root, other domains as scopes, locales in URL form', () => {
    const files = written.map((file) => file.file.slice(catalogueDir.length + 1)).sort();
    assert.deepEqual(files, ['en.json', 'ru.json', 'seo/en.json', 'zh-hans.json']);
    assert.equal(read('kept.json').constructor, Object);
  });

  it('keeps keys and their nesting, and turns scalars into text', () => {
    const en = read('en.json');
    assert.equal(en.nested.deeper.deepest, "Patch {{ version }} of l'Atelier");
    assert.equal(en.patch, '42');
    assert.equal(
      read('seo', 'en.json').home.title,
      'All {{ count }} champions — patch {{ version }}',
    );
  });

  it('leaves email.* to the server', () => {
    assert.equal('email' in read('en.json'), false);
    assert.equal(written.find((file) => file.file.endsWith('/en.json')).excludedKeys, 1);
  });

  it('merges X and X_one into one ICU plural, and keeps an orphan X_one', () => {
    const { filter } = read('en.json');
    assert.deepEqual(Object.keys(filter), ['results', 'orphan_one']);
    const render = (count) => renderLikeTransloco(filter.results, { count }, 'en');
    assert.deepEqual([1, 2].map(render), ['1 result', '2 results']);
  });

  it('converts pipe plurals of every locale', () => {
    const apples = read('en.json').apples;
    const render = (count) => renderLikeTransloco(apples, { count, name: 'Ornn' }, 'en');
    assert.deepEqual([0, 1, 7].map(render), ['No apples', 'One apple', '7 apples for Ornn']);
    assert.equal(renderLikeTransloco(read('ru.json').apples, { count: 22 }, 'ru'), '22 яблока');
  });

  it('keeps quoted syntax and plain pipes as text', () => {
    const en = read('en.json');
    assert.equal(
      renderLikeTransloco(en.nested.syntax, {}, 'en'),
      "Braces {like this}, a hash # and it''s quoted",
    );
    assert.equal(en.pipe_text, 'Left || right, no count here|really');
  });

  it('reports what it converted', () => {
    const en = written.find((file) => file.file.endsWith('/en.json'));
    assert.deepEqual(
      { messages: en.messages, pluralPairs: en.pluralPairs, pipePlurals: en.pipePlurals },
      { messages: 9, pluralPairs: 1, pipePlurals: 2 },
    );
  });

  it('refuses a domain it does not know', () => {
    const yamlDir = mkdtempSync(join(tmpdir(), 'lodb-yaml-'));
    writeFileSync(join(yamlDir, 'validators.en.yaml'), 'a: b\n');
    try {
      assert.throws(() => runConversion({ yamlDir, catalogueDir }), /Unknown translation domain/);
    } finally {
      rmSync(yamlDir, { recursive: true, force: true });
    }
  });
});
