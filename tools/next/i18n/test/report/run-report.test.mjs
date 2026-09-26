import assert from 'node:assert/strict';
import { mkdirSync, mkdtempSync, rmSync, writeFileSync } from 'node:fs';
import { tmpdir } from 'node:os';
import { join } from 'node:path';
import { afterEach, beforeEach, describe, it } from 'node:test';
import { runReport } from '../../lib/report/run-report.mjs';

describe('runReport', () => {
  let dir;
  const write = (path, catalogue) => {
    mkdirSync(join(dir, path, '..'), { recursive: true });
    writeFileSync(join(dir, path), JSON.stringify(catalogue));
  };

  beforeEach(() => {
    dir = mkdtempSync(join(tmpdir(), 'lodb-report-'));
    write('en.json', { a: { b: 'B', c: 'C' }, d: 'D' });
    write('fr.json', { a: { b: 'B fr', c: 'C fr' }, d: 'D fr' });
    write('de.json', { a: { b: 'B de' }, stale: 'old' });
    write('ja.json', { a: { b: '{broken', c: 'C' }, d: 'D' });
    write('seo/en.json', { title: 'T' });
    write('seo/fr.json', { title: 'T fr' });
    write('seo/ja.json', { title: 'T ja' });
  });

  afterEach(() => rmSync(dir, { recursive: true, force: true }));

  it('counts missing keys per locale and scope against en', () => {
    const { markdown } = runReport(dir);

    assert.match(markdown, /4 locales, 2 portées \(racine, seo\), 4 clés dans la référence `en`/);
    assert.match(markdown, /Locales complètes : 3 ; incomplètes : 1\./);
    assert.match(markdown, /\| de \| 1 \/ 4 \| 3 \| 25,0 % \| 2 \| 1 \|/);
    assert.match(markdown, /\| fr \| 4 \/ 4 \| 0 \| 100,0 % \| 0 \| 0 \|/);
  });

  it('lists missing keys grouped by the locales lacking them', () => {
    const { markdown } = runReport(dir);

    assert.match(markdown, /### racine\n\nClés manquantes : 2, dans de\.[\s\S]*- `a\.c`\n- `d`/);
    assert.match(markdown, /### seo\n\nClés manquantes : 1, dans de\./);
  });

  it('reports absent files, keys unknown to en and messages ICU rejects', () => {
    const { markdown, summary } = runReport(dir);

    assert.match(markdown, /## Fichiers absents\n\n- seo : `de`/);
    assert.match(markdown, /## Clés hors référence\n\n- racine, `de` : `stale`/);
    assert.match(markdown, /## Messages ICU invalides\n\n- racine, `ja`, `a\.b` : /);
    assert.deepEqual(summary, [
      'root/de: 2 missing, 1 extra, 0 invalid',
      'root/ja: 0 missing, 0 extra, 1 invalid',
      'seo/de: 1 missing, 0 extra, 0 invalid',
    ]);
  });

  it('gives the same report twice, so an unchanged run leaves the file untouched', () => {
    assert.equal(runReport(dir).markdown, runReport(dir).markdown);
  });

  it('fails when a scope has no reference catalogue', () => {
    rmSync(join(dir, 'seo', 'en.json'));

    assert.throws(() => runReport(dir), /Scope "seo" has no en catalogue/);
  });
});
