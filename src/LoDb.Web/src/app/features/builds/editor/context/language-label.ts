/**
 * The name of a Data Dragon language in that language itself (`Français (France)`), so an
 * author finds their own whatever page they are on; the code when `Intl` cannot name it.
 * The context switcher names its languages alike, in its own feature.
 */
export function languageLabel(language: string): string {
  const tag = language.replace('_', '-');
  try {
    const names = new Intl.DisplayNames([tag], { type: 'language', languageDisplay: 'standard' });
    const name = names.of(tag);
    return name ? name.charAt(0).toLocaleUpperCase(tag) + name.slice(1) : language;
  } catch {
    // An ill-formed tag: the select still offers the language, under its code.
    return language;
  }
}
