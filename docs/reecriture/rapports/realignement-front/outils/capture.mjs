// Usage: node capture.mjs <outDir> <pairsJson> [widths=1440,390] [only=name1,name2]
// Captures full-page screenshots + visible text of legacy (:8080) vs next (:18080) page pairs.
// Pair: {name, old, new, oldBase?, newBase?, scheme?: 'dark'|'light', click?: [selectorOld, selectorNew]}
import { createRequire } from 'node:module';
import fs from 'node:fs';
import path from 'node:path';
const require = createRequire('F:/Git/LeagueOfDataBaseFinal/tests/LoDb.E2E/package.json');
const { chromium } = require('playwright');

const [, , outDir, pairsFile, widthsArg, onlyArg] = process.argv;
let pairs = JSON.parse(fs.readFileSync(pairsFile, 'utf8'));
if (onlyArg) pairs = pairs.filter((p) => onlyArg.split(',').includes(p.name));
const widths = (widthsArg ?? '1440,390').split(',').map(Number);
fs.mkdirSync(outDir, { recursive: true });

const OLD = process.env.OLD_BASE ?? 'http://localhost:8080';
const NEW = process.env.NEW_BASE ?? 'http://localhost:18080';
const HIDE_DEV_CHROME = '.sf-toolbar, .sf-minitoolbar, [id^="sfwdt"] { display: none !important; }';

const browser = await chromium.launch();
for (const width of widths) {
  for (const p of pairs) {
    const ctx = await browser.newContext({
      viewport: { width, height: width > 800 ? 900 : 844 },
      deviceScaleFactor: 1,
      colorScheme: p.scheme ?? 'dark',
      locale: p.browserLocale ?? 'en-US',
      isMobile: width < 800,
      hasTouch: width < 800,
    });
    const sides = [
      ['old', p.oldBase ?? OLD, p.old, p.click?.[0]],
      ['new', p.newBase ?? NEW, p.new, p.click?.[1]],
    ];
    for (const [side, base, url, click] of sides) {
      if (!url) continue;
      const page = await ctx.newPage();
      try {
        await page.goto(base + url, { waitUntil: 'networkidle', timeout: 90000 });
        await page.addStyleTag({ content: HIDE_DEV_CHROME });
        if (click) {
          await page.click(click, { timeout: 10000 });
          await page.waitForTimeout(600);
        }
        await page.waitForTimeout(900);
        const stem = path.join(outDir, `${p.name}-${width}-${side}`);
        await page.screenshot({ path: `${stem}.png`, fullPage: !click });
        const text = await page.evaluate(() => document.body.innerText);
        fs.writeFileSync(`${stem}.txt`, `URL: ${page.url()}\nTITLE: ${await page.title()}\n\n${text}`);
        console.log('ok', stem);
      } catch (e) {
        console.log('fail', p.name, side, url, e.message.split('\n')[0]);
      } finally {
        await page.close();
      }
    }
    await ctx.close();
  }
}
await browser.close();
