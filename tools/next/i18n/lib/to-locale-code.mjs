/**
 * Maps a Symfony catalogue locale to its URL form (ADR 0005): `zh_Hans` → `zh-hans`.
 * Every other locale is already a lowercase two-letter code.
 */
export function toLocaleCode(symfonyLocale) {
  return symfonyLocale.replace('_', '-').toLowerCase();
}
