// Runs Lighthouse in the Chrome of Playwright, with the default (mobile) profile. Lighthouse,
// chrome-launcher and Playwright are dependencies of tests/LoDb.E2E: they are resolved from
// there, so the tool needs `npm ci` in tests/LoDb.E2E and nothing else.

import { createRequire } from 'node:module';
import { pathToFileURL } from 'node:url';
import { metricsOf } from './metrics.mjs';
import { summarize } from './summary.mjs';

const CATEGORIES = ['performance', 'accessibility', 'best-practices', 'seo'];
const CHROME_FLAGS = ['--headless=new', '--no-first-run', '--no-default-browser-check'];
// Third parties a page must never reach from this tool (none is known to load today).
const BLOCKED_URLS = [
  '*.google-analytics.com',
  '*.googletagmanager.com',
  '*.googlesyndication.com',
  '*.doubleclick.net',
  '*.stripe.com',
];
const WARM_UP_TIMEOUT_MS = 30_000;

async function loadTools(e2ePackage) {
  const require = createRequire(e2ePackage);
  const load = async (name) => import(pathToFileURL(require.resolve(name)).href);
  const [{ default: lighthouse }, launcher] = await Promise.all([
    load('lighthouse'),
    load('chrome-launcher'),
  ]);
  const { chromium } = require('@playwright/test');
  return { lighthouse, launcher, chromePath: chromium.executablePath() };
}

// A first request renders the page and fills nginx's cache; it is not measured.
async function warmUp(url) {
  const response = await fetch(url, { signal: AbortSignal.timeout(WARM_UP_TIMEOUT_MS) });
  await response.arrayBuffer();
  if (!response.ok) throw new Error(`${url} answered ${response.status}`);
}

async function measurePage(tools, port, url, runs) {
  await warmUp(url);
  const flags = { port, logLevel: 'error', onlyCategories: CATEGORIES };
  const config = { extends: 'lighthouse:default', settings: { blockedUrlPatterns: BLOCKED_URLS } };
  const results = [];
  for (let run = 0; run < runs; run += 1) {
    const { lhr } = await tools.lighthouse(url, flags, config);
    results.push(metricsOf(lhr));
  }
  return summarize(results);
}

/**
 * The median figures of each page (`{ name, path }`) of a site, `runs` Lighthouse runs each,
 * one at a time, in a Chrome launched for the site.
 */
export async function measureSite(site, pages, options) {
  const tools = await loadTools(options.e2ePackage);
  const chrome = await tools.launcher.launch({
    chromePath: tools.chromePath,
    chromeFlags: CHROME_FLAGS,
  });
  try {
    const measured = [];
    for (const page of pages) {
      const url = new URL(page.path, site).href;
      const figures = await measurePage(tools, chrome.port, url, options.runs);
      measured.push({ ...page, url, ...figures });
    }
    return measured;
  } finally {
    chrome.kill();
  }
}
