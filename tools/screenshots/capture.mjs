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

const TARGETS = [
  ['01-setup', '/'],
  ['02-home', '/home'],
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

const HIDE_TOOLBAR = '.sf-toolbar,.sf-minitoolbar{display:none!important}'

async function settle(page) {
  try { await page.waitForLoadState('networkidle', { timeout: 8000 }) } catch {}
  try { await page.evaluate(() => document.fonts?.ready) } catch {}
  await page.addStyleTag({ content: HIDE_TOOLBAR }).catch(() => {})
  await page.waitForTimeout(450)
}

async function shoot(ctx, name, url, full = true) {
  const page = await ctx.newPage()
  const results = []
  try {
    const resp = await page.goto(BASE + url, { waitUntil: 'domcontentloaded', timeout: 45000 })
    const status = resp?.status() ?? 0
    await settle(page)
    const file = path.join(OUT, `${name}.png`)
    await page.screenshot({ path: file, fullPage: full })
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
  const desktop = await browser.newContext({ viewport: { width: 1440, height: 900 }, deviceScaleFactor: 1 })
  console.log(`\nDesktop 1440 → ${OUT}`)
  for (const [name, url] of TARGETS) console.log(await shoot(desktop, name, url))
  await desktop.close()

  // Mobile (a representative subset)
  const mobile = await browser.newContext({ viewport: { width: 390, height: 844 }, deviceScaleFactor: 2 })
  console.log(`\nMobile 390`)
  for (const [name, url] of [TARGETS[0], TARGETS[1], TARGETS[2], TARGETS[6]]) {
    console.log(await shoot(mobile, `${name}-mobile`, url))
  }
  await mobile.close()

  await browser.close()
  console.log('\nDone.')
}

run().catch((e) => { console.error(e); process.exit(1) })
