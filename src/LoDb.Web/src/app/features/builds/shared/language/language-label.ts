/**
 * The name of a Data Dragon language in that language itself (`Français (France)`), from
 * `Intl`, never from a table copied by hand; the code itself when `Intl` cannot name it. The
 * context switcher has its twin; a feature cannot share it.
 */
export function languageLabel(language: string): string {
  const tag = language.replace('_', '-');
  try {
    const names = new Intl.DisplayNames([tag], { type: 'language', languageDisplay: 'standard' });
    const name = names.of(tag);
    return name ? name.charAt(0).toLocaleUpperCase(tag) + name.slice(1) : language;
  } catch {
    // An ill-formed tag: the chip still shows the language, under its code.
    return language;
  }
}
