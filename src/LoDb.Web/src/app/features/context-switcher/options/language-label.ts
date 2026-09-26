/** BCP 47 tag of a Data Dragon language: `en-GB` for `en_GB`. */
export function languageTag(language: string): string {
  return language.replace('_', '-');
}

/**
 * The name of a Data Dragon language in that language itself (`English (United Kingdom)`,
 * `Français (France)`), so visitors find their own whatever page they are on. It comes from
 * `Intl`, never from a table copied by hand; the code itself when `Intl` cannot name it.
 */
export function languageLabel(language: string): string {
  const tag = languageTag(language);
  try {
    const names = new Intl.DisplayNames([tag], { type: 'language', languageDisplay: 'standard' });
    const name = names.of(tag);
    return name ? name.charAt(0).toLocaleUpperCase(tag) + name.slice(1) : language;
  } catch {
    // An ill-formed tag: the list still offers the language, under its code.
    return language;
  }
}
