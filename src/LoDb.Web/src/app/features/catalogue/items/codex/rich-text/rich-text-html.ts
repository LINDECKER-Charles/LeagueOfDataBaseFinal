import { RICH_TEXT_TAGS } from './rich-text-tags';

// A tag as Data Dragon writes one: a name, then attributes the output never keeps.
const TAG = /<\s*(\/?)\s*([a-zA-Z][a-zA-Z0-9-]*)[^<>]*>/g;
// Markup characters of the text, and any `&` that does not start an entity it already spells.
const UNSAFE = /[<>"]|&(?!(?:#\d+|#x[0-9a-fA-F]+|[a-zA-Z][a-zA-Z0-9]*);)/g;
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

function tagOf(isClosing: boolean, rawName: string): string {
  const name = rawName.toLowerCase();
  if (!RICH_TEXT_TAGS.has(name)) {
    return '';
  }
  if (VOID_TAGS.has(name)) {
    return isClosing ? '' : `<${name}>`;
  }
  return isClosing ? `</${name}>` : `<${name}>`;
}

/**
 * Riot's rich text rebuilt from an allow-list: the vocabulary foundation/ddragon.css styles
 * (`<stats>`, `<passive>`, `<magicDamage>`…) keeps its tags, stripped of every attribute;
 * any other tag goes, its text kept; the text itself is escaped. What comes out can hold no
 * script, no handler and no URL, so it may be trusted as HTML, which Angular's sanitizer
 * would otherwise strip of the game's own tags.
 */
export function richTextHtml(text: string): string {
  let html = '';
  let last = 0;
  for (const match of text.matchAll(TAG)) {
    html += escapeText(text.slice(last, match.index)) + tagOf(match[1] === '/', match[2] ?? '');
    last = match.index + match[0].length;
  }
  return html + escapeText(text.slice(last));
}
