import assert from 'node:assert/strict';
import { test } from 'node:test';
import { classifyPair } from '../lib/classify.mjs';
import { parseHead } from '../lib/head.mjs';

const PROD = 'https://league-of-data-base.com';
const ORIGINS = { prod: PROD, next: ['http://localhost', PROD] };

function html({ title, canonical, jsonLd = [] }) {
  const scripts = jsonLd.map(
    (block) => `<script type="application/ld+json">${JSON.stringify(block)}</script>`,
  );
  const link = `<link rel="canonical" href="${canonical}">`;
  return `<html><head><title>${title}</title>${link}${scripts.join('')}</head></html>`;
}

function page(title, canonical, url) {
  return { title, canonical, jsonLd: url === undefined ? [] : [{ '@type': 'VideoGame', url }] };
}

function pairOf(prodUrl, nextUrl, pages) {
  return {
    prodUrl,
    nextUrl,
    latest: '16.19.1',
    origins: ORIGINS,
    hop: { status: 301, location: new URL(nextUrl).pathname },
    prod: { status: 200, head: parseHead(html(pages.prod)) },
    next: { status: 200, head: parseHead(html(pages.next)) },
  };
}

function outcomes(findings) {
  return Object.fromEntries(findings.map((finding) => [finding.field, finding.outcome]));
}

test('finds a page and its equivalent the same, grammar and origin apart', () => {
  const next = 'http://localhost/en/champions/Annie';
  const pair = pairOf('/champion/Annie', next, {
    prod: page('Annie', `${PROD}/champion/Annie`, `${PROD}/champion/Annie`),
    next: page('Annie', next, next),
  });

  const verdicts = Object.values(outcomes(classifyPair(pair)));

  assert.deepEqual(verdicts, ['same', 'same', 'same', 'same', 'same', 'same']);
});

test('explains the translated title of a localized page, and nothing else', () => {
  const pair = pairOf('/champion/Annie?lang=fr_FR', 'http://localhost/fr/champions/Annie', {
    prod: page('Annie, LoL champion', `${PROD}/champion/Annie`),
    next: page('Annie, champion de LoL', 'http://localhost/fr/champions/Annie?x=1'),
  });

  const findings = classifyPair(pair);
  const title = findings.find((finding) => finding.field === 'title');

  assert.equal(title.outcome, 'explained');
  assert.deepEqual(title.rules.map((rule) => rule.id), ['locale-in-url']);
  assert.equal(outcomes(findings).canonical, 'unexplained');
});

test('leaves a difference no rule knows unexplained', () => {
  const pair = pairOf('/object/3031', 'http://localhost/en/items/3031-infinity-edge', {
    prod: page('Infinity Edge', `${PROD}/object/3031`),
    next: page('Infinity Blade', 'http://localhost/en/items/3031-infinity-edge'),
  });

  assert.equal(outcomes(classifyPair(pair)).title, 'unexplained');
});

test('reports a redirect that lands on another page', () => {
  const pair = pairOf('/object/3031', 'http://localhost/en/items/3032-other', {
    prod: page('x', `${PROD}/object/3031`),
    next: page('x', 'http://localhost/en/items/3032-other'),
  });

  const findings = classifyPair(pair);

  assert.equal(outcomes(findings).redirect, 'unexplained');
  assert.equal(outcomes(findings).canonical, 'unexplained');
});
