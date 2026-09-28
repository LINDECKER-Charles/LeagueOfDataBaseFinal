/**
 * The text with its first letter in upper case, by the rules of `locale` (a Turkish `i`
 * becomes `İ`): Data Dragon writes titles in lower case, "the Darkin Blade".
 */
export function capitalize(text: string, locale: string): string {
  const [first = '', ...rest] = [...text];
  return first.toLocaleUpperCase(locale) + rest.join('');
}
