// Compares the head of a production page with the head of its equivalent on the stack, field
// by field. A finding says `same` or `differs`, with both values; explaining a difference is
// rules.mjs's job, not this one's.

import { diffBlocks, normalizeJsonLd, typesOf, urlRewriter } from './json-ld.mjs';
import { legacyKey, nextKey } from './page-key.mjs';

/** The fields of a pair, in the order of the report. */
export const FIELDS = ['redirect', 'title', 'description', 'canonical', 'types', 'fields'];

const MOVED_PERMANENTLY = 301;
const OK = 200;

function finding(field, prod, next, extra = {}) {
  const same = JSON.stringify(prod) === JSON.stringify(next);
  return { field, verdict: same ? 'same' : 'differs', prod, next, ...extra };
}

// The legacy URL, asked of the stack, must reach the same page in a single 301: the page it
// lands on has the key of the production page, and answers without redirecting again.
function redirectFinding(pair) {
  const { hop, next, prodUrl, latest } = pair;
  const expected = { status: MOVED_PERMANENTLY, key: legacyKey(prodUrl, latest), landing: OK };
  const landed = hop.location === null ? null : nextKey(hop.location, latest).key;
  return finding('redirect', expected, { status: hop.status, key: landed, landing: next.status });
}

// Canonical problems of the stack's page that no key comparison would show.
function canonicalFaults(nextCanonicals, landingLocale, latest) {
  const faults = [];
  if (nextCanonicals.length !== 1) faults.push(`${nextCanonicals.length} canonicals`);
  const canonical = nextCanonicals[0] ?? '';
  if (canonical.includes('?')) faults.push('query in the canonical');
  if (canonical !== '' && nextKey(canonical, latest).locale !== landingLocale) {
    faults.push('canonical outside the locale of the page');
  }
  return faults;
}

function canonicalFinding(pair) {
  const { prod, next, latest, nextUrl } = pair;
  const prodCanonical = prod.head.canonicals[0] ?? null;
  const nextCanonical = next.head.canonicals[0] ?? null;
  const prodKey = prodCanonical === null ? null : legacyKey(prodCanonical, latest);
  const nextCanonicalKey = nextCanonical === null ? null : nextKey(nextCanonical, latest).key;
  const landingLocale = nextUrl === null ? null : nextKey(nextUrl).locale;
  const faults = canonicalFaults(next.head.canonicals, landingLocale, latest);
  const compared = finding('canonical', prodKey, nextCanonicalKey, {
    urls: { prod: prodCanonical, next: nextCanonical },
    faults,
  });
  return faults.length === 0 ? compared : { ...compared, verdict: 'differs' };
}

function normalizedBlocks(pair) {
  const { prod, next, latest, origins } = pair;
  const prodRewrite = urlRewriter([origins.prod], (path) => legacyKey(path, latest));
  const nextRewrite = urlRewriter(origins.next, (path) => nextKey(path, latest).key);
  return {
    prod: normalizeJsonLd(prod.head.jsonLd, prodRewrite),
    next: normalizeJsonLd(next.head.jsonLd, nextRewrite),
  };
}

function jsonLdFindings(pair) {
  const blocks = normalizedBlocks(pair);
  const differences = diffBlocks(blocks.prod, blocks.next);
  const errors = [...pair.prod.head.jsonLdErrors, ...pair.next.head.jsonLdErrors];
  return [
    finding('types', typesOf(blocks.prod), typesOf(blocks.next), { errors }),
    { field: 'fields', verdict: differences.length === 0 ? 'same' : 'differs', differences },
  ];
}

/**
 * Findings of one pair. `pair` holds the production and stack pages (`status`, `head`), the
 * stack's answer to the legacy URL (`hop`), the landing URL (`nextUrl`), the latest version,
 * and the origins of both sites (`origins.prod`, `origins.next[]`).
 */
export function comparePair(pair) {
  const { prod, next } = pair;
  return [
    redirectFinding(pair),
    finding('title', prod.head.title, next.head.title),
    finding('description', prod.head.description, next.head.description),
    canonicalFinding(pair),
    ...jsonLdFindings(pair),
  ];
}
