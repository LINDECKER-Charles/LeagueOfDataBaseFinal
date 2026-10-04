// Reads the /b/{token} pages of a stack in Playwright's Chromium, which tests/LoDb.E2E
// provides: the tool needs `npm ci` there and nothing else. Scripts are off: both stacks
// render the page on the server, and the comparison reads that render, as a crawler or a
// link preview does, free of timing.

import { createRequire } from 'node:module';
import { extractFacts } from './extract.mjs';

const NAVIGATION_TIMEOUT_MS = 30_000;

/** Playwright's Chromium, resolved from the package.json of the end-to-end suite. */
export function chromiumOf(e2ePackage) {
  const require = createRequire(e2ePackage);
  return require('@playwright/test').chromium;
}

// `fr_FR` → `fr-FR`, the browser's language: the legacy stack speaks the visitor's.
function browserLocale(language) {
  return language.replace('_', '-');
}

/**
 * The status and facts of each page of `builds` on the stack at `base`, one at a time, each
 * asked with `?lang=` set to the build's language and a browser speaking it.
 */
export async function readStack(browser, base, builds, pauseMs = 0) {
  const readings = [];
  for (const build of builds) {
    const context = await browser.newContext({
      javaScriptEnabled: false,
      locale: browserLocale(build.language),
    });
    try {
      const page = await context.newPage();
      const url = new URL(`/b/${build.token}?lang=${build.language}`, base).href;
      const response = await page.goto(url, { timeout: NAVIGATION_TIMEOUT_MS });
      const status = response?.status() ?? 0;
      const facts = status === 200 ? await page.evaluate(extractFacts) : null;
      readings.push({ token: build.token, url, status, facts });
    } finally {
      await context.close();
    }
    if (pauseMs > 0) await new Promise((resolve) => setTimeout(resolve, pauseMs));
  }
  return readings;
}
