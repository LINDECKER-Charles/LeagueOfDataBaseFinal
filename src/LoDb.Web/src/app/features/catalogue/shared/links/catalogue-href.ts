import type { PageContext } from '../../../../core/context/page-context';
import { QueryString } from '../../../../core/routing/url/query-string';
import { searchOf } from '../url/search-of';

/** The regional variant of a page (`?lang=en_GB`): the only parameter a link carries on. */
const LANG_PARAM = 'lang';

/**
 * The URL of a catalogue page in the reader's context: `/{locale}/[{version}/]{path}`, the
 * version only when pinned, and the `?lang=` of the current URL, if any, carried on.
 * `path` is the API's `canonicalPath` (`champions/Aatrox`) or a list (`champions`).
 */
export function catalogueHref(context: PageContext, path: string, currentUrl = ''): string {
  const version = context.pinned ? `${context.version}/` : '';
  const lang = QueryString.parse(searchOf(currentUrl)).get(LANG_PARAM);
  const query = lang ? QueryString.parse('').with(LANG_PARAM, lang).toString() : '';
  return `/${context.locale}/${version}${path}${query}`;
}
