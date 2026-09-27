// Reads the former site's sitemaps: an index (/sitemap.xml) or a list of URLs, from a URL or
// from a file saved before the switch. Only <loc> matters; the XML is the old site's own
// (SitemapBuilder.php), flat and without namespace prefixes.

import { readFile } from 'node:fs/promises';

const ENTITIES = { amp: '&', lt: '<', gt: '>', quot: '"', apos: "'" };

function decode(text) {
  return text.replace(/&(amp|lt|gt|quot|apos);/g, (_, name) => ENTITIES[name]);
}

/** The kind of a sitemap document and its <loc> values, in order. */
export function parseSitemap(xml) {
  const kind = /<sitemapindex[\s>]/.test(xml) ? 'index' : /<urlset[\s>]/.test(xml) ? 'urlset' : null;
  if (kind === null) {
    throw new Error('neither a <sitemapindex> nor a <urlset>');
  }
  const locs = [...xml.matchAll(/<loc>\s*([^<]*?)\s*<\/loc>/g)].map((match) => decode(match[1]));
  return { kind, locs };
}

/** Path and query of a former URL, whatever host the old site wrote into it. */
export function pathOf(loc) {
  const url = new URL(loc, 'http://former.invalid');
  return `${url.pathname}${url.search}`;
}

/**
 * The sitemaps of an index to follow: the primary one (/sitemaps/latest.xml), then the first
 * `historical` version sitemaps, in the index's order (newest first).
 */
export function pickSitemaps(locs, historical) {
  const primary = locs.filter((loc) => pathOf(loc) === '/sitemaps/latest.xml');
  const versions = locs.filter((loc) => pathOf(loc) !== '/sitemaps/latest.xml');
  return [...primary, ...versions.slice(0, historical)];
}

/** At most `size` entries spread evenly over the list, first and last included; all at 0. */
export function sample(list, size) {
  if (size <= 0 || list.length <= size) {
    return [...list];
  }
  if (size === 1) {
    return [list[0]];
  }
  const step = (list.length - 1) / (size - 1);
  return Array.from({ length: size }, (_, index) => list[Math.round(index * step)]);
}

/**
 * Reads a sitemap from `source`, a URL or a file. A relative child of a file is read from
 * `origin`, the former site; a child of a URL from that URL's origin, since the old site wrote
 * its public host into every <loc>.
 */
export async function readSitemap(source, { origin, timeoutMs }) {
  if (/^https?:\/\//.test(source)) {
    const response = await fetch(source, { signal: AbortSignal.timeout(timeoutMs) });
    if (!response.ok) {
      throw new Error(`${source} answered ${response.status}`);
    }
    return { ...parseSitemap(await response.text()), origin: new URL(source).origin };
  }
  return { ...parseSitemap(await readFile(source, 'utf8')), origin };
}
