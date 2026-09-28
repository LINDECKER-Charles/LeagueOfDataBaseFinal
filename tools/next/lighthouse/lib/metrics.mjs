// What the report keeps of a Lighthouse result (lhr): the scores, the lab metrics, the audits
// of accessibility and SEO that fail, the performance audits worth reading, and the text
// resources served without compression (Lighthouse 13 no longer has an audit for them).

const SCORES = {
  performance: 'performance',
  accessibility: 'accessibility',
  bestPractices: 'best-practices',
  seo: 'seo',
};
const LAB = {
  fcp: 'first-contentful-paint',
  lcp: 'largest-contentful-paint',
  tbt: 'total-blocking-time',
  cls: 'cumulative-layout-shift',
  speedIndex: 'speed-index',
};
const CHECKED_CATEGORIES = ['accessibility', 'seo'];
const WORTH_READING = 0.9;
const PERCENT = 100;
const TEXT_TYPES = new Set(['Document', 'Script', 'Stylesheet', 'Fetch', 'Manifest']);
const MIN_COMPRESSIBLE = 1024;

function score(category) {
  return category?.score === null || category === undefined
    ? null
    : Math.round(category.score * PERCENT);
}

function failingAudits(lhr) {
  return CHECKED_CATEGORIES.flatMap((id) =>
    lhr.categories[id].auditRefs
      .filter((ref) => ref.weight > 0 && lhr.audits[ref.id].score !== null)
      .filter((ref) => lhr.audits[ref.id].score < 1)
      .map((ref) => ({ category: id, id: ref.id, title: lhr.audits[ref.id].title })),
  );
}

function leads(lhr) {
  return lhr.categories.performance.auditRefs
    .map((ref) => lhr.audits[ref.id])
    .filter((audit) => audit.score !== null && audit.score < WORTH_READING)
    .filter((audit) => !Object.values(LAB).includes(audit.id))
    .map((audit) => ({ id: audit.id, title: audit.title, value: audit.displayValue ?? '' }));
}

function uncompressed(lhr) {
  const origin = new URL(lhr.finalDisplayedUrl).origin;
  return lhr.audits['network-requests'].details.items
    .filter((item) => item.url.startsWith(origin) && TEXT_TYPES.has(item.resourceType))
    .filter((item) => item.resourceSize >= MIN_COMPRESSIBLE)
    .filter((item) => item.transferSize >= item.resourceSize)
    .map((item) => ({ url: item.url.slice(origin.length), bytes: item.resourceSize }));
}

/** The figures of one run; throws when Lighthouse could not load the page. */
export function metricsOf(lhr) {
  if (lhr.runtimeError !== undefined) {
    throw new Error(`${lhr.requestedUrl}: ${lhr.runtimeError.code} ${lhr.runtimeError.message}`);
  }
  const scores = Object.entries(SCORES).map(([key, id]) => [key, score(lhr.categories[id])]);
  const lab = Object.entries(LAB).map(([key, id]) => [key, lhr.audits[id].numericValue]);
  return {
    ...Object.fromEntries([...scores, ...lab]),
    failing: failingAudits(lhr),
    leads: leads(lhr),
    uncompressed: uncompressed(lhr),
    warnings: lhr.runWarnings,
    environment: { lighthouse: lhr.lighthouseVersion, userAgent: lhr.environment.hostUserAgent },
  };
}

/** The numeric figures of a run, the ones a median is taken of. */
export const NUMERIC = [...Object.keys(SCORES), ...Object.keys(LAB)];
