import type { Locale } from '../../i18n/locales';
import { QueryString } from './query-string';

const LANG_PARAM = 'lang';

/**
 * The address of a catalogue page (ADR 0005): `/{locale}/[{version}/]{path}`, the version
 * only when pinned, then the language variant as `?lang=`. `path` is a list (`champions`)
 * or an entity's `canonicalPath` (`champions/Aatrox`). The one place that decides where a
 * version and a variant go in a catalogue link, for the pages and the chrome alike.
 */
export function cataloguePath(
  locale: Locale,
  path: string,
  context: { readonly version: string | null; readonly lang: string | null },
): string {
  const version = context.version === null ? '' : `${context.version}/`;
  const query = context.lang ? QueryString.parse('').with(LANG_PARAM, context.lang).toString() : '';
  return `/${locale}/${version}${path}${query}`;
}
