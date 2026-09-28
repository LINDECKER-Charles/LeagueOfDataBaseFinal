// Tags that separate words (Riot writes `<br>` between sentences); any other tag wraps a
// word and is dropped without a space, so "<b>Data Dragon</b>." stays "Data Dragon.".
const BREAK = /<\s*\/?\s*(?:br|p|li|div)\b[^>]*>/gi;
const TAG = /<[^>]*>/g;
const SPACES = /\s+/g;

/**
 * A structured-data text from Riot's rich text: markup dropped, whitespace collapsed, and
 * null when nothing is left. The API already removed the unresolved template tokens.
 */
export function plainText(value: string | null | undefined): string | null {
  const text = (value ?? '').replace(BREAK, ' ').replace(TAG, '').replace(SPACES, ' ').trim();
  return text === '' ? null : text;
}
