import assert from 'node:assert/strict';
import { test } from 'node:test';
import { classifyPair } from '../lib/classify.mjs';
import { parseHead } from '../lib/head.mjs';

const PROD = 'https://league-of-data-base.com';
const NEXT = 'http://localhost/en/about/data';

function head(canonical, inLanguage) {
  const dataset = { '@type': 'Dataset', name: 'LoDb', inLanguage };
  const script = `<script type="application/ld+json">${JSON.stringify(dataset)}</script>`;
  return parseHead(
    `<html><head><title>Data</title><link rel="canonical" href="${canonical}">${script}</head></html>`,
  );
}

function aboutDataPair(prodLanguages, nextLanguages) {
  return {
    prodUrl: '/about/data',
    nextUrl: NEXT,
    latest: '16.19.1',
    origins: { prod: PROD, next: ['http://localhost', PROD] },
    hop: { status: 301, location: '/en/about/data' },
    prod: { status: 200, head: head(`${PROD}/about/data`, prodLanguages) },
    next: { status: 200, head: head(NEXT, nextLanguages) },
  };
}

function fieldsOf(pair) {
  return classifyPair(pair).find((finding) => finding.field === 'fields');
}

test('explains the Data Dragon languages written as BCP 47 tags', () => {
  const fields = fieldsOf(aboutDataPair(['ar_AE', 'en_US', 'zh_TW'], ['ar-AE', 'en-US', 'zh-TW']));

  assert.equal(fields.outcome, 'explained');
  assert.deepEqual(
    fields.rules.map((rule) => [rule.id, rule.kind]),
    [['dataset-languages', 'correction']],
  );
});

test('leaves another language, or a missing one, unexplained', () => {
  const fields = fieldsOf(aboutDataPair(['ar_AE', 'en_US', 'zh_TW'], ['ar', 'en-US']));

  assert.equal(fields.outcome, 'unexplained');
  assert.deepEqual(
    fields.unexplained.map((difference) => difference.path),
    ['Dataset.inLanguage[0]', 'Dataset.inLanguage[2]'],
  );
});
