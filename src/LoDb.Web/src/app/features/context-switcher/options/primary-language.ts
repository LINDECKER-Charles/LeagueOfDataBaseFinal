const REGION_SEPARATOR = '_';

/**
 * `en` for `en_GB`: a locale accepts the regional variants of its own language, never another
 * language (ADR 0005), and `/api/meta` gives no other link between them.
 */
export function primaryLanguage(language: string): string {
  return language.split(REGION_SEPARATOR, 1)[0];
}
