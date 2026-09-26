import type { JsonLdNode } from './json-ld-node';

// Characters that must never reach a <script> element verbatim: `<` and `>` could close it
// (`</script>`) or open a comment, `&` could start an entity in an XHTML parser, and the two
// line separators end a JavaScript string in older engines.
const UNSAFE = /[<>&\u2028\u2029]/g;

function escape(character: string): string {
  return `\\u${character.charCodeAt(0).toString(16).padStart(4, '0')}`;
}

/**
 * The text of a `<script type="application/ld+json">`: JSON whose unsafe characters are
 * written as `\uXXXX` escapes. This is the security boundary: names and descriptions come
 * from Data Dragon and from users, and must never inject markup. Slashes and non-ASCII
 * letters stay verbatim, so the URLs and the 21 locales remain readable.
 */
export function serializeJsonLd(node: JsonLdNode): string {
  return JSON.stringify(node).replace(UNSAFE, escape);
}
