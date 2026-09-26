import assert from 'node:assert/strict';
import { test } from 'node:test';
import { checkBudgets } from '../lib/budgets.mjs';
import { metricsOf } from '../lib/metrics.mjs';
import { renderReport } from '../lib/report.mjs';
import { median, summarize } from '../lib/summary.mjs';

const ORIGIN = 'http://localhost:18180';

function audit(id, score, extra = {}) {
  return { id, title: `title of ${id}`, score, ...extra };
}

function request(url, resourceType, transferSize) {
  return { url, resourceType, resourceSize: 5000, transferSize };
}

function auditsOf({ lcp, cls }) {
  return [
    audit('first-contentful-paint', 0.9, { numericValue: 1500 }),
    audit('largest-contentful-paint', 0.9, { numericValue: lcp }),
    audit('total-blocking-time', 1, { numericValue: 40 }),
    audit('cumulative-layout-shift', 1, { numericValue: cls }),
    audit('speed-index', 1, { numericValue: 1800 }),
    audit('unused-javascript', 0.5, { displayValue: 'Est savings of 124 KiB' }),
    audit('color-contrast', 0),
    audit('document-title', 1),
    audit('network-requests', null, {
      details: {
        items: [
          request(`${ORIGIN}/build/main.js`, 'Script', 5300),
          request(`${ORIGIN}/build/styles.css`, 'Stylesheet', 900),
          request(`${ORIGIN}/cdn/blobs/a.webp`, 'Image', 5300),
          request('https://cdn.example/lib.js', 'Script', 5300),
        ],
      },
    }),
  ];
}

function lhrOf({ performance = 0.95, lcp = 2000, cls = 0.01 } = {}) {
  const audits = auditsOf({ lcp, cls });
  const refs = (ids) => ids.map((id) => ({ id, weight: 1 }));
  return {
    requestedUrl: `${ORIGIN}/en/`,
    finalDisplayedUrl: `${ORIGIN}/en/`,
    lighthouseVersion: '13.5.0',
    environment: { hostUserAgent: 'HeadlessChrome/153' },
    runWarnings: [],
    audits: Object.fromEntries(audits.map((entry) => [entry.id, entry])),
    categories: {
      performance: {
        score: performance,
        auditRefs: refs(['largest-contentful-paint', 'unused-javascript']),
      },
      accessibility: { score: 0.93, auditRefs: refs(['color-contrast']) },
      'best-practices': { score: 1, auditRefs: [] },
      seo: { score: null, auditRefs: refs(['document-title']) },
    },
  };
}

test('keeps the scores, the lab metrics and what explains them', () => {
  const run = metricsOf(lhrOf());

  assert.equal(run.performance, 95);
  assert.equal(run.accessibility, 93);
  assert.equal(run.seo, null);
  assert.equal(run.lcp, 2000);
  assert.deepEqual(run.failing.map((entry) => entry.id), ['color-contrast']);
  assert.deepEqual(run.leads.map((entry) => entry.id), ['unused-javascript']);
  assert.deepEqual(run.uncompressed, [{ url: '/build/main.js', bytes: 5000 }]);
});

test('refuses a run where Lighthouse could not load the page', () => {
  const lhr = { ...lhrOf(), runtimeError: { code: 'ERRORED_DOCUMENT_REQUEST', message: '404' } };

  assert.throws(() => metricsOf(lhr), /ERRORED_DOCUMENT_REQUEST/);
});

test('takes the median of each figure and the details of the median run', () => {
  const runs = [0.7, 0.9, 0.8].map((performance, index) =>
    metricsOf(lhrOf({ performance, lcp: 1000 * (index + 1) })),
  );

  const page = summarize(runs);

  assert.equal(median([3, 1, 2]), 2);
  assert.equal(median([4, 1, 2, 3]), 2.5);
  assert.equal(median([]), null);
  assert.equal(page.performance, 80);
  assert.equal(page.lcp, 2000);
  assert.equal(page.runs, 3);
});

test('checks each budget, a missing figure failing its budget', () => {
  const metrics = { performance: 90, accessibility: 94, seo: null, lcp: 2500, cls: 0.2 };

  const checks = checkBudgets(metrics);

  assert.deepEqual(
    Object.fromEntries(checks.map((check) => [check.metric, check.met])),
    { performance: true, accessibility: false, seo: false, lcp: true, cls: false },
  );
});

test('reports the pages, their verdict and what the median run found', () => {
  const page = { name: 'Accueil', url: `${ORIGIN}/en/`, ...summarize([metricsOf(lhrOf())]) };
  const meta = { date: '2026-09-26', next: ORIGIN, stack: 'e1', runs: 1, command: 'node run.mjs' };

  const report = renderReport(meta, [page], []);

  assert.match(report, /\| \[Accueil\]\(http:\/\/localhost:18180\/en\/\) \| 95 \| 93 \|/);
  assert.match(report, /\*\*✗\*\* Accessibilité, SEO \|/);
  assert.match(report, /budgets manqués sur 1 page\(s\) sur 1 : Accueil\./);
  assert.match(report, /`color-contrast`/);
  assert.match(report, /1 ressource\(s\) texte servie\(s\) sans compression, 5 Kio/);
  assert.match(report, /sans compression, 5 Kio : `\/build\/main\.js`\./);
  assert.doesNotMatch(report, /Prod, à titre indicatif/);
});
