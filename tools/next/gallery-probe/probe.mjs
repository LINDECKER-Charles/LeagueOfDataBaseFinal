// Gallery probe of chantier L3.2: loads the development gallery in the four themes, left to
// right and right to left, at 320, 390, 768 and 1440px; checks the painted identity, the
// direction and that nothing overflows the screen; drives the theme picker, the mirrored
// tabs and a dialog; saves one full-page capture per view.
//
// Usage, with the Angular dev server running (npm start --prefix src/LoDb.Web):
//   node tools/next/gallery-probe/probe.mjs [--no-captures]
// GALLERY_URL overrides the server address (default http://127.0.0.1:4200). Playwright
// and its Chromium come from tests/LoDb.E2E (npm ci, then npm run browsers:install there).
import { mkdir, writeFile } from 'node:fs/promises';
import { createRequire } from 'node:module';
import path from 'node:path';
import { fileURLToPath } from 'node:url';
import {
  mirroredTabsFailures,
  overlayDirectionFailures,
  themePickerFailures,
} from './interaction-checks.mjs';
import {
  GALLERY_PATH,
  LOCALES,
  MOBILE_UP_TO,
  NARROWEST,
  THEMES,
  VIEWPORT_HEIGHT,
  WIDTHS,
} from './matrix.mjs';
import {
  accentColour,
  directionFailures,
  overflowFailures,
  themeFailures,
} from './page-checks.mjs';

const ROOT = path.resolve(path.dirname(fileURLToPath(import.meta.url)), '../../..');
const { chromium } = createRequire(path.join(ROOT, 'tests/LoDb.E2E/package.json'))(
  '@playwright/test',
);
const BASE_URL = process.env.GALLERY_URL ?? 'http://127.0.0.1:4200';
const CAPTURE_DIR = path.join(ROOT, 'docs/reecriture/rapports/captures/design-system');
const CAPTURES = !process.argv.includes('--no-captures');
const JPEG_QUALITY = 70;

function label({ theme, locale, width }) {
  return `${theme.name} · ${locale.dir} · ${width}px`;
}

async function openGallery(browser, { theme, locale, width }) {
  const mobile = width <= MOBILE_UP_TO;
  const context = await browser.newContext({
    viewport: { width, height: VIEWPORT_HEIGHT },
    isMobile: mobile,
    hasTouch: mobile,
    // Settles the reveal and the ambient loops, so every capture shows the whole page.
    reducedMotion: 'reduce',
  });
  await context.addCookies([{ name: 'lod_theme', value: theme.name, url: BASE_URL }]);
  const page = await context.newPage();
  const errors = [];
  page.on('pageerror', (error) => errors.push(`page error: ${error.message}`));
  await page.goto(`${BASE_URL}/${locale.locale}${GALLERY_PATH}`, {
    waitUntil: 'load',
  });
  // Image states are stamped after the browser render: the app has hydrated.
  await page.locator('.hx-img[data-img-state]').first().waitFor({ state: 'attached' });
  await page.evaluate(() => document.fonts.ready);
  return { context, page, errors };
}

async function capture(page, { theme, locale, width }) {
  const file = path.join(CAPTURE_DIR, `${theme.name}-${locale.dir}-${width}.jpg`);
  await page.screenshot({
    path: file,
    fullPage: true,
    type: 'jpeg',
    quality: JPEG_QUALITY,
  });
}

async function probeView(browser, view, accents) {
  const { context, page, errors } = await openGallery(browser, view);
  try {
    const failures = [
      ...(await themeFailures(page, view.theme)),
      ...(await directionFailures(page, view.locale.dir)),
      ...(await overflowFailures(page, view.width)),
      ...errors,
    ];
    accents.set(view.theme.name, await accentColour(page));
    if (CAPTURES) await capture(page, view);
    return { name: label(view), failures };
  } finally {
    await context.close();
  }
}

async function interact(browser, view, check) {
  const { context, page, errors } = await openGallery(browser, view);
  try {
    const failures = [...(await check(page)), ...errors];
    return { name: `${check.name} · ${label(view)}`, failures };
  } finally {
    await context.close();
  }
}

function interactions(browser) {
  const [hextech] = THEMES;
  const [ltr, rtl] = LOCALES;
  return [
    () => interact(browser, { theme: hextech, locale: ltr, width: 390 }, themePickerFailures),
    () => interact(browser, { theme: hextech, locale: rtl, width: 1440 }, mirroredTabsFailures),
    () =>
      interact(
        browser,
        { theme: hextech, locale: rtl, width: NARROWEST },
        function rtlDialog(page) {
          return overlayDirectionFailures(page, 'rtl');
        },
      ),
    () =>
      interact(
        browser,
        { theme: hextech, locale: ltr, width: NARROWEST },
        function ltrDialog(page) {
          return overlayDirectionFailures(page, 'ltr');
        },
      ),
  ];
}

function accentResult(accents) {
  const distinct = new Set(accents.values()).size;
  const failures =
    distinct === THEMES.length ? [] : [`${distinct} distinct accents for ${THEMES.length} themes`];
  return { name: `accents ${[...accents.values()].join(' ')}`, failures };
}

async function run(browser) {
  const results = [];
  const accents = new Map();
  for (const theme of THEMES) {
    for (const locale of LOCALES) {
      for (const width of WIDTHS) {
        results.push(await probeView(browser, { theme, locale, width }, accents));
      }
    }
  }
  for (const probe of interactions(browser)) {
    results.push(await probe());
  }
  return [...results, accentResult(accents)];
}

async function report(results) {
  for (const { name, failures } of results) {
    console.log(
      failures.length === 0 ? `ok   ${name}` : `FAIL ${name}\n     ${failures.join('\n     ')}`,
    );
  }
  const failed = results.filter((result) => result.failures.length > 0).length;
  console.log(`\n${results.length - failed}/${results.length} checks passed against ${BASE_URL}`);
  const summary = {
    baseUrl: BASE_URL,
    date: new Date().toISOString(),
    results,
  };
  await writeFile(
    path.join(CAPTURE_DIR, 'probe-report.json'),
    `${JSON.stringify(summary, null, 2)}\n`,
  );
  return failed;
}

async function main() {
  await mkdir(CAPTURE_DIR, { recursive: true });
  const browser = await chromium.launch();
  try {
    const failed = await report(await run(browser));
    process.exitCode = failed === 0 ? 0 : 1;
  } finally {
    await browser.close();
  }
}

await main();
