// The SEO facts of a page's <head>, read from the HTML the server sent: what a crawler reads.
// A tolerant reader, not a parser: both sites write well-formed heads, and a tag it cannot
// read counts as missing, which the diff then reports.

const NAMED_ENTITIES = { amp: '&', lt: '<', gt: '>', quot: '"', apos: "'", nbsp: ' ' };
const ENTITY = /&(?:#(\d+)|#x([0-9a-f]+)|([a-z]+));/gi;
const ATTRIBUTE = /([^\s=/>]+)(?:\s*=\s*(?:"([^"]*)"|'([^']*)'|([^\s>]+)))?/g;
const DECIMAL = 10;
const HEXADECIMAL = 16;

/** Decodes the character references an attribute or a title may carry. */
export function decodeEntities(text) {
  return text.replace(ENTITY, (whole, decimal, hexadecimal, name) => {
    if (decimal !== undefined) return String.fromCodePoint(Number.parseInt(decimal, DECIMAL));
    if (hexadecimal !== undefined) {
      return String.fromCodePoint(Number.parseInt(hexadecimal, HEXADECIMAL));
    }
    return NAMED_ENTITIES[name.toLowerCase()] ?? whole;
  });
}

/** The attributes of one start tag, names lower-cased, values decoded. */
export function attributesOf(tag) {
  const inner = tag.replace(/^<\s*[a-z0-9-]+/i, '').replace(/\/?>$/, '');
  const attributes = {};
  for (const match of inner.matchAll(ATTRIBUTE)) {
    const value = match[2] ?? match[3] ?? match[4] ?? '';
    attributes[match[1].toLowerCase()] = decodeEntities(value);
  }
  return attributes;
}

function headOf(html) {
  const end = html.search(/<\/head>/i);
  return end < 0 ? html : html.slice(0, end);
}

function tagsOf(head, name) {
  return [...head.matchAll(new RegExp(`<${name}\\b[^>]*>`, 'gi'))].map((m) => attributesOf(m[0]));
}

function relIncludes(link, rel) {
  return (link.rel ?? '').toLowerCase().split(/\s+/).includes(rel);
}

function metaContent(metas, name) {
  const meta = metas.find((candidate) => (candidate.name ?? '').toLowerCase() === name);
  return meta === undefined ? null : (meta.content ?? '');
}

function titleOf(head) {
  const match = head.match(/<title\b[^>]*>([\s\S]*?)<\/title>/i);
  return match === null ? null : decodeEntities(match[1]).replace(/\s+/g, ' ').trim();
}

// Each JSON-LD block, parsed; a block that does not parse is kept as an error.
function jsonLdOf(head) {
  const blocks = [];
  const errors = [];
  for (const match of head.matchAll(/<script\b([^>]*)>([\s\S]*?)<\/script>/gi)) {
    if ((attributesOf(`<script ${match[1]}>`).type ?? '') !== 'application/ld+json') continue;
    try {
      blocks.push(JSON.parse(match[2]));
    } catch (error) {
      errors.push(error.message);
    }
  }
  return { blocks, errors };
}

function htmlLangOf(html) {
  const match = html.match(/<html\b[^>]*>/i);
  return match === null ? null : (attributesOf(match[0]).lang ?? null);
}

/**
 * The facts the diff compares: title, description, canonical, robots, hreflang alternates,
 * the language of the document and its JSON-LD blocks.
 */
export function parseHead(html) {
  const head = headOf(html);
  const links = tagsOf(head, 'link');
  const metas = tagsOf(head, 'meta');
  const canonicals = links.filter((link) => relIncludes(link, 'canonical'));
  const { blocks, errors } = jsonLdOf(head);
  return {
    title: titleOf(head),
    description: metaContent(metas, 'description'),
    robots: metaContent(metas, 'robots'),
    canonicals: canonicals.map((link) => link.href ?? ''),
    hreflangs: links
      .filter((link) => relIncludes(link, 'alternate') && link.hreflang !== undefined)
      .map((link) => ({ lang: link.hreflang, href: link.href ?? '' })),
    htmlLang: htmlLangOf(html),
    jsonLd: blocks,
    jsonLdErrors: errors,
  };
}
