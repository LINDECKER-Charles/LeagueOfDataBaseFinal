// Checks the legacy 301 (plan-migration.md, table of the legacy 301; L8.2): every URL of the
// former site's sitemaps, or of a list, must reach its new form on the stack at --base in one
// 301, then answer 200 there. `/` answers a 302 to a locale, the contracts are never moved.
//
//   node tools/cutover/check-301.mjs --base <new stack> (--sitemap <url|file>)... \
//        [--list <file>]... [--legacy <origin>] [--historical 1] [--sample 0] \
//        [--concurrency 4] [--timeout-ms 180000] [--json <file>]
//
// --sitemap  the old /sitemap.xml (followed: the primary sitemap and --historical version
//            sitemaps) or one of its sitemaps, as a URL or a file saved before the switch;
//            the URLs of the index and of the sitemaps followed are checked too. A child of
//            a file is read from --legacy.
// --list     a file of former URLs or paths, one per line; `#` starts a comment.
// --sample   at most this many URLs per sitemap, spread evenly (0: all).
//
// The first URL of each sitemap goes alone: a cold version is ingested on demand by the API,
// which the others then find ready. Prints one line per failure and a summary; exit code 0
// when every URL passes, 1 when one fails, 2 on invalid arguments or an unreadable sitemap.
import { readFile, writeFile } from 'node:fs/promises';
import { parseArgs } from 'node:util';
import { pathOf, pickSitemaps, readSitemap, sample } from './lib/sitemaps.mjs';
import { pool, verify } from './lib/verify.mjs';

const { values } = parseArgs({
  options: {
    base: { type: 'string', default: process.env.LODB_E2E_BASE_URL ?? 'http://localhost:18080' },
    sitemap: { type: 'string', multiple: true, default: [] },
    list: { type: 'string', multiple: true, default: [] },
    legacy: { type: 'string' },
    historical: { type: 'string', default: '1' },
    sample: { type: 'string', default: '0' },
    concurrency: { type: 'string', default: '4' },
    'timeout-ms': { type: 'string', default: '180000' },
    json: { type: 'string' },
  },
});

const timeoutMs = Number(values['timeout-ms']);
const historical = Number(values.historical);
const sampleSize = Number(values.sample);
const concurrency = Number(values.concurrency);
if (values.sitemap.length === 0 && values.list.length === 0) {
  console.error('Give at least one --sitemap or --list.');
  process.exit(2);
}

// Groups of paths, each checked after its first one: a sitemap, or a list.
async function collect() {
  const groups = [];
  for (const source of values.sitemap) {
    const document = await readSitemap(source, { origin: values.legacy, timeoutMs });
    if (document.kind === 'urlset') {
      groups.push({ name: source, paths: sample(document.locs.map(pathOf), sampleSize) });
      continue;
    }
    const children = pickSitemaps(document.locs, historical);
    // The index and the sitemaps followed are former URLs as well. Not the others: each
    // version sitemap of the new stack reads its version, which a cold one ingests.
    groups.push({ name: `${source} (sitemaps)`, paths: ['/sitemap.xml', ...children.map(pathOf)] });
    for (const child of children) {
      if (document.origin === undefined) {
        throw new Error(`${source} is a file: give --legacy to read ${child}`);
      }
      const url = new URL(pathOf(child), document.origin).href;
      const urlset = await readSitemap(url, { origin: document.origin, timeoutMs });
      groups.push({ name: url, paths: sample(urlset.locs.map(pathOf), sampleSize) });
    }
  }
  for (const file of values.list) {
    const lines = (await readFile(file, 'utf8')).split('\n');
    const paths = lines.map((line) => line.replace(/#.*/, '').trim()).filter(Boolean).map(pathOf);
    groups.push({ name: file, paths });
  }
  return groups;
}

let groups;
try {
  groups = await collect();
} catch (error) {
  console.error(`Cannot read the former URLs: ${error.message}`);
  process.exit(2);
}

const results = [];
for (const group of groups) {
  const unique = [...new Set(group.paths)];
  const started = performance.now();
  const [first, ...rest] = unique;
  const verdicts = first === undefined ? [] : [await verify(first, { base: values.base, timeoutMs })];
  verdicts.push(...(await pool(rest, concurrency, (path) => verify(path, { base: values.base, timeoutMs }))));
  const failed = verdicts.filter((verdict) => !verdict.ok);
  for (const verdict of failed) {
    console.log(`FAIL ${verdict.path}: ${verdict.reason}`);
  }
  const seconds = ((performance.now() - started) / 1000).toFixed(1);
  console.log(`${group.name}: ${verdicts.length - failed.length}/${verdicts.length} ok in ${seconds} s`);
  results.push(...verdicts);
}

const failures = results.filter((verdict) => !verdict.ok).length;
const kinds = Object.entries(
  results.reduce((counts, verdict) => ({ ...counts, [verdict.kind]: (counts[verdict.kind] ?? 0) + 1 }), {}),
)
  .map(([kind, count]) => `${kind} ${count}`)
  .join(', ');
console.log(`${results.length - failures}/${results.length} former URLs pass (${kinds}).`);
if (values.json !== undefined) {
  await writeFile(values.json, `${JSON.stringify(results, null, 2)}\n`);
}
process.exitCode = failures === 0 ? 0 : 1;
