import { createHash } from 'node:crypto';

// A script element and its raw text: the HTML parser ends script data at the first `</script`,
// and Angular escapes that sequence inside the state it serializes.
const SCRIPT = /<script\b([^>]*)>([\s\S]*?)<\/script\s*>/gi;
const SOURCE_ATTRIBUTE = /(?:^|\s)src\s*=/i;
const TYPE_ATTRIBUTE = /(?:^|\s)type\s*=\s*(?:"([^"]*)"|'([^']*)'|([^\s>]+))/i;
// The types a browser executes (HTML's JavaScript MIME type essences, plus modules). Data
// blocks such as JSON-LD or Angular's transfer state run nothing and need no hash.
const EXECUTABLE_TYPE =
  /^(?:module|(?:text|application)\/(?:x-)?(?:java|ecma)script|text\/javascript1\.[0-5]|text\/(?:jscript|livescript))$/i;

function isExecutableInline(attributes: string): boolean {
  if (SOURCE_ATTRIBUTE.test(attributes)) {
    return false;
  }
  const type = TYPE_ATTRIBUTE.exec(attributes);
  const value = (type?.[1] ?? type?.[2] ?? type?.[3] ?? '').split(';')[0].trim();
  return value === '' || EXECUTABLE_TYPE.test(value);
}

/**
 * CSP sources (`'sha256-…'`) of the inline scripts a page executes, in document order and
 * without repeats. Hashed on the server for each page, because Angular's `autoCsp` refuses
 * SSR builds, and some of these scripts are written at render time (event replay).
 */
export function inlineScriptHashes(html: string): string[] {
  const hashes = new Set<string>();
  for (const [, attributes, text] of html.matchAll(SCRIPT)) {
    if (isExecutableInline(attributes)) {
      hashes.add(`'sha256-${createHash('sha256').update(text, 'utf8').digest('base64')}'`);
    }
  }
  return [...hashes];
}
