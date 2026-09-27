import { RICH_TEXT_TAGS } from './rich-text-tags';

// A comment, a tag (its name, then attributes), or a lone `<` that opens neither.
const MARKUP = /<!--[\s\S]*?(?:-->|$)|<(\/?)([a-zA-Z][\w-]*)\b([^<>]*)>|</g;
// Markup characters of the text, and any `&` that does not start an entity it already spells.
const UNSAFE = /[<>"]|&(?!(?:#\d+|#x[0-9a-fA-F]+|[a-zA-Z][a-zA-Z0-9]*);)/g;
// Riot's colour hint: a hex colour and nothing else, so no script, URL or style can pass.
const FONT_COLOR = /\bcolor\s*=\s*(["']?)(#(?:[\da-f]{8}|[\da-f]{6}|[\da-f]{3,4}))\1(?=[\s/]|$)/i;
// Elements without content: a closing tag of theirs means nothing.
const VOID_TAGS: ReadonlySet<string> = new Set(['br', 'hr']);
const ESCAPES: Readonly<Record<string, string>> = {
  '<': '&lt;',
  '>': '&gt;',
  '"': '&quot;',
  '&': '&amp;',
};

function escapeText(text: string): string {
  return text.replace(UNSAFE, (char) => ESCAPES[char] ?? char);
}

/** An opening tag, bare: only `<font>` keeps one attribute, a colour it has checked. */
function openingTag(name: string, attributes: string): string {
  const color = name === 'font' ? FONT_COLOR.exec(attributes)?.[2] : undefined;
  return color === undefined ? `<${name}>` : `<font color="${color}">`;
}

function tagOf(isClosing: boolean, rawName: string, attributes: string): string {
  const name = rawName.toLowerCase();
  if (!RICH_TEXT_TAGS.has(name)) {
    return '';
  }
  if (VOID_TAGS.has(name)) {
    return isClosing ? '' : `<${name}>`;
  }
  return isClosing ? `</${name}>` : openingTag(name, attributes);
}

/**
 * Riot's rich text rebuilt from an allow-list: the vocabulary foundation/ddragon.css styles
 * (`<stats>`, `<passive>`, `<magicDamage>`…) keeps its tags, stripped of every attribute
 * but the hex colour of a `<font>`, the hint Data Dragon gives a keyword (teal "adaptive
 * damage"); any other tag and any comment go, their text kept; the text itself is escaped.
 * What comes out can hold no script, no handler and no URL, so it may be trusted as HTML,
 * which Angular's sanitizer would otherwise strip of the game's own tags. Pure, so the
 * server and the browser write the same HTML.
 */
export function richTextHtml(text: string): string {
  let html = '';
  let last = 0;
  for (const match of text.matchAll(MARKUP)) {
    const [token, closing, name, attributes] = match;
    html += escapeText(text.slice(last, match.index));
    if (name !== undefined) {
      html += tagOf(closing === '/', name, attributes ?? '');
    } else if (token === '<') {
      html += '&lt;';
    }
    last = match.index + token.length;
  }
  return html + escapeText(text.slice(last));
}
