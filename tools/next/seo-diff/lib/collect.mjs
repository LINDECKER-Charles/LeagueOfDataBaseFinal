// Reads the pages of the sample from both sites. Production is only read: GET requests, one
// at a time, with a pause between them, no cookie, no redirect followed.

import { setTimeout as sleep } from 'node:timers/promises';
import { parseHead } from './head.mjs';
import { legacyUrlOf } from './sample.mjs';

const USER_AGENT = 'LoDb-seo-diff/1.0 (read-only comparison of the rewrite)';
const REQUEST_TIMEOUT_MS = 30_000;

async function get(url) {
  const response = await fetch(url, {
    redirect: 'manual',
    headers: { 'User-Agent': USER_AGENT, 'Accept-Language': 'en' },
    signal: AbortSignal.timeout(REQUEST_TIMEOUT_MS),
  });
  return {
    status: response.status,
    location: response.headers.get('location'),
    html: await response.text(),
  };
}

async function page(url) {
  const { status, html } = await get(url);
  return { status, head: parseHead(html) };
}

// The stack's answer to the legacy URL, then the page its 301 names.
async function stackSide(base, url) {
  const hop = await get(new URL(legacyUrlOf(url), base).href);
  const nextUrl = hop.location === null ? null : new URL(hop.location, base).href;
  const next =
    nextUrl === null ? { status: hop.status, head: parseHead(hop.html) } : await page(nextUrl);
  return { hop: { status: hop.status, location: hop.location }, nextUrl, next };
}

/**
 * Reads every entry of the sample: production page, stack redirect and stack page. `sites`
 * gives both origins; `pauseMs` spaces the requests sent to production.
 */
export async function collect(sample, sites, pauseMs) {
  const pairs = [];
  for (const entry of sample) {
    const prod = await page(new URL(entry.url, sites.prod).href);
    const stack = await stackSide(sites.next, entry.url);
    pairs.push({ ...entry, prodUrl: entry.url, prod, ...stack });
    await sleep(pauseMs);
  }
  return pairs;
}
