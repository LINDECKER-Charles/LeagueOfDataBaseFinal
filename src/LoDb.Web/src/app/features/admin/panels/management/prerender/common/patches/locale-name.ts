// The admin reads in French whatever the locale of the site (shared/admin-i18n.ts).
const LANGUAGE = 'fr';
const SCRIPT_LENGTH = 4;
const REGION_LENGTH = 2;

let names: Intl.DisplayNames | null | undefined;

// Built once, lazily: a runtime without the display names keeps the codes.
function displayNames(): Intl.DisplayNames | null {
  if (names === undefined) {
    try {
      names = new Intl.DisplayNames(LANGUAGE, { type: 'language' });
    } catch {
      names = null;
    }
  }
  return names;
}

// A code of the URLs as a BCP 47 tag: `zh-hans` → `zh-Hans`, `pt-br` → `pt-BR`.
function tagOf(code: string): string {
  return code
    .split('-')
    .map((part, index) => {
      if (index === 0) {
        return part.toLowerCase();
      }
      if (part.length === SCRIPT_LENGTH) {
        return part[0].toUpperCase() + part.slice(1).toLowerCase();
      }
      return part.length === REGION_LENGTH ? part.toUpperCase() : part;
    })
    .join('-');
}

/** The French name of a locale of the site, `fr` → français; the code itself when unknown. */
export function localeName(code: string): string {
  try {
    return displayNames()?.of(tagOf(code)) ?? code;
  } catch {
    return code;
  }
}
