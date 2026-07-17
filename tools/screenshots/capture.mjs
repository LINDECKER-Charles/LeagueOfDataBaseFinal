// Playwright capture of the LoDB UI into /screenshot.
// Usage: BASE_URL=http://localhost:8080 node capture.mjs
import { chromium } from 'playwright'
import { mkdir } from 'node:fs/promises'
import { fileURLToPath } from 'node:url'
import path from 'node:path'

const BASE = process.env.BASE_URL ?? 'http://localhost:8080'
const V = process.env.LODB_VERSION ?? '16.14.1'
const L = process.env.LODB_LANG ?? 'en_US'
const OUT = path.resolve(path.dirname(fileURLToPath(import.meta.url)), '..', '..', 'screenshot')

const q = (pp) => `?version=${V}&lang=${L}&numpage=1&itemperpage=${pp}`
const d = `?version=${V}&lang=${L}` // detail routes fall back to setup without a version

// Third tuple element = per-shot options:
//   full: false        → viewport shot instead of full page
//   openSwitcher: true → open the header patch/language popover before shooting
// The version/language picker is no longer a standalone page: it lives in the
// header disclosure, so "01-setup" is the home with that popover open.
const TARGETS = [
  ['01-setup', '/', { full: false, openSwitcher: true }],
  ['02-home', '/'],
  ['03-champions', `/champions${q(20)}`],
  ['04-objects', `/objects${q(8)}`],
  ['05-runes', `/runes${q(8)}`],
  ['06-summoners', `/summoners${q(8)}`],
  ['07-champion-detail', `/champion/Aatrox${d}`],
  ['08-object-detail', `/object/1001${d}`],
  ['09-rune-detail', `/rune/Domination${d}`],
  ['10-summoner-detail', `/summoner/SummonerBarrier${d}`],
  ['11-working', '/working-progress'],
]

// Hide the Symfony web-debug toolbar and the floating perf badge (a dev-only
// affordance that overlaps content) so captures show product UI only.
const HIDE_CHROME = '.sf-toolbar,.sf-minitoolbar,#load-time-badge,.hx-perf{display:none!important}'

// [data-reveal] sections and loading="lazy" images only activate on scroll.
// Reduced-motion (context option) force-reveals the sections at mount; flipping
// every lazy <img> to eager loads them all — including off-screen ones like the
// horizontal skins carousel — without a scroll pass that would drift the
// scrollspy nav off the top section.
async function eagerLoadImages(page) {
  await page.evaluate(() => {
    document.querySelectorAll('img[loading="lazy"]').forEach((img) => { img.loading = 'eager' })
  })
}

// Bounded: a lazy <img> parked off-screen may never fire load/error, so race
// the "all decoded" promise against a hard cap to guarantee the shot proceeds.
async function waitForImages(page, timeout = 6000) {
  await page
    .evaluate(
      (t) =>
        Promise.race([
          Promise.all(
            Array.from(document.images).map((img) =>
              img.complete ? Promise.resolve() : new Promise((res) => { img.onload = img.onerror = () => res() }),
            ),
          ),
          new Promise((res) => setTimeout(res, t)),
        ]),
      timeout,
    )
    .catch(() => {})
}

async function settle(page) {
  await page.addStyleTag({ content: HIDE_CHROME }).catch(() => {})
  try { await page.waitForLoadState('networkidle', { timeout: 5000 }) } catch {}
  await eagerLoadImages(page) // after islands mount, so their imgs are caught too
  try { await page.waitForLoadState('networkidle', { timeout: 5000 }) } catch {}
  await waitForImages(page)
  try { await page.evaluate(() => document.fonts?.ready) } catch {}
  await page.waitForTimeout(400)
}

async function shoot(ctx, name, url, opts = {}) {
  const page = await ctx.newPage()
  const results = []
  try {
    const resp = await page.goto(BASE + url, { waitUntil: 'domcontentloaded', timeout: 45000 })
    const status = resp?.status() ?? 0
    await settle(page)
    if (opts.openSwitcher) {
      await page.evaluate(() => {
        const el = document.querySelector('details.switcher')
        if (el) el.open = true
      })
      await page.waitForTimeout(350)
    }
    const file = path.join(OUT, `${name}.png`)
    await page.screenshot({ path: file, fullPage: opts.full !== false })
    results.push(`  ✓ ${name.padEnd(20)} ${String(status).padStart(3)}  ${url}`)
  } catch (e) {
    results.push(`  ✗ ${name.padEnd(20)} ERR  ${url}  (${e.message.split('\n')[0]})`)
  } finally {
    await page.close()
  }
  return results.join('\n')
}

// Warm the server (Symfony container + on-demand image resolution) so the timed
// captures don't eat the cold-start cost. 'commit' resolves once headers arrive,
// i.e. after the full server-side render.
async function warm(ctx) {
  const page = await ctx.newPage()
  for (const [, url] of TARGETS) {
    try { await page.goto(BASE + url, { waitUntil: 'commit', timeout: 90000 }) } catch {}
  }
  await page.close()
}

async function run() {
  await mkdir(OUT, { recursive: true })
  const browser = await chromium.launch()

  const warmer = await browser.newContext()
  console.log('Warming…')
  await warm(warmer)
  await warmer.close()

  // Desktop
  const desktop = await browser.newContext({
    viewport: { width: 1440, height: 900 },
    deviceScaleFactor: 1,
    reducedMotion: 'reduce',
  })
  console.log(`\nDesktop 1440 → ${OUT}`)
  for (const [name, url, opts] of TARGETS) console.log(await shoot(desktop, name, url, opts))
  await desktop.close()

  // Mobile (a representative subset)
  const mobile = await browser.newContext({
    viewport: { width: 390, height: 844 },
    deviceScaleFactor: 2,
    reducedMotion: 'reduce',
  })
  console.log(`\nMobile 390`)
  for (const [name, url, opts] of [TARGETS[0], TARGETS[1], TARGETS[2], TARGETS[6]]) {
    console.log(await shoot(mobile, `${name}-mobile`, url, opts))
  }
  await mobile.close()

  await browser.close()
  console.log('\nDone.')
}

run().catch((e) => { console.error(e); process.exit(1) })
