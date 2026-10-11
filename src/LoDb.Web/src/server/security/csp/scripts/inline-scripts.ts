// A script element and its raw text: the HTML parser ends script data at the first `</script`
// followed by a space, `/` or `>`, whatever attributes the end tag then carries; Angular
// escapes that sequence inside the state it serializes.
const SCRIPT = /<script\b([^>]*)>([\s\S]*?)<\/script(?:[\s/][^>]*)?>/gi;
const SOURCE_ATTRIBUTE = /(?:^|\s)src\s*=/i;
const TYPE_ATTRIBUTE = /(?:^|\s)type\s*=\s*(?:"([^"]*)"|'([^']*)'|([^\s>]+))/i;
// The types a browser executes (HTML's JavaScript MIME type essences, plus modules). Data
// blocks such as JSON-LD or Angular's transfer state run nothing and need no hash.
const EXECUTABLE_TYPE =
  /^(?:module|(?:text|application)\/(?:x-)?(?:java|ecma)script|text\/javascript1\.[0-5]|text\/(?:jscript|livescript))$/i;

/** An inline script a browser would run, where the document holds it. */
export interface InlineScript {
  /** The attributes of its opening tag, as written. */
  readonly attributes: string;
  /** Its text, byte for byte what a CSP hash covers. */
  readonly text: string;
  /** Offset of its opening `<script`. */
  readonly start: number;
  /** Offset right after its closing tag. */
  readonly end: number;
}

function isExecutableInline(attributes: string): boolean {
  if (SOURCE_ATTRIBUTE.test(attributes)) {
    return false;
  }
  const type = TYPE_ATTRIBUTE.exec(attributes);
  const value = (type?.[1] ?? type?.[2] ?? type?.[3] ?? '').split(';')[0].trim();
  return value === '' || EXECUTABLE_TYPE.test(value);
}

/** The inline scripts `html` executes, in document order. */
export function inlineScripts(html: string): InlineScript[] {
  const scripts: InlineScript[] = [];
  for (const match of html.matchAll(SCRIPT)) {
    const [element, attributes, text] = match;
    if (isExecutableInline(attributes)) {
      scripts.push({ attributes, text, start: match.index, end: match.index + element.length });
    }
  }
  return scripts;
}
