const PLACEHOLDER = /%([A-Za-z_][A-Za-z0-9_]*)%/g;
const TRANSLOCO_DELIMITER = /{{|}}/;

/**
 * Splits a Symfony message into literal text and `%param%` placeholders. A literal `{{` or
 * `}}` is refused: Transloco would read it as interpolation before MessageFormat could
 * unquote it, and no catalogue needs one.
 */
export function tokenizeMessage(source) {
  if (TRANSLOCO_DELIMITER.test(source)) {
    throw new Error(`Literal "{{" or "}}" cannot survive Transloco interpolation: ${source}`);
  }
  const tokens = [];
  let cursor = 0;
  for (const match of source.matchAll(PLACEHOLDER)) {
    if (match.index > cursor) {
      tokens.push({ kind: 'text', text: source.slice(cursor, match.index) });
    }
    tokens.push({ kind: 'param', name: match[1] });
    cursor = match.index + match[0].length;
  }
  if (cursor < source.length) {
    tokens.push({ kind: 'text', text: source.slice(cursor) });
  }
  return tokens;
}
