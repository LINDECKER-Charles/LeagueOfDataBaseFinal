import { DDRAGON_TAGS } from './ddragon-tags';

/** Void elements: written without a closing tag. */
const VOID_TAGS: ReadonlySet<string> = new Set(['br', 'hr']);
/** A tag, a comment, or a lone `<` that opens neither. */
const MARKUP = /<!--[\s\S]*?(?:-->|$)|<(\/?)([a-zA-Z][\w-]*)\b[^>]*>|</g;
/** An `&` that already starts a character reference is kept as written. */
const BARE_AMPERSAND = /&(?!#\d+;|#x[\da-fA-F]+;|[a-zA-Z][a-zA-Z\d]*;)/g;

function escapeText(text: string): string {
  return text.replace(BARE_AMPERSAND, '&amp;').replace(/>/g, '&gt;').replace(/"/g, '&quot;');
}

function tagOf(closing: string, name: string): string {
  const tag = name.toLowerCase();
  if (!DDRAGON_TAGS.has(tag)) {
    return '';
  }
  if (VOID_TAGS.has(tag)) {
    return closing ? '' : `<${tag}>`;
  }
  return `<${closing}${tag}>`;
}

/**
 * Data Dragon rich text rewritten to the markup it may keep: the tags of DDRAGON_TAGS, every
 * attribute dropped; any other tag and any comment removed, their text kept; the text
 * escaped. Pure, so the server and the browser write the same HTML, and safe to hand to
 * `innerHTML` as is: nothing it returns can run a script or load a resource.
 */
export function ddragonHtml(raw: string): string {
  let html = '';
  let last = 0;
  for (const match of raw.matchAll(MARKUP)) {
    html += escapeText(raw.slice(last, match.index));
    const [token, closing, name] = match;
    html += name === undefined ? (token === '<' ? '&lt;' : '') : tagOf(closing ?? '', name);
    last = match.index + token.length;
  }
  return html + escapeText(raw.slice(last));
}
