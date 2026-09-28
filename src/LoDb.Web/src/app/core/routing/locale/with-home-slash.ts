import { isLocale } from '../../i18n/is-locale';

// Where the path of a URL ends: at its query or at its fragment.
const PATH_END = /[?#]/;

/**
 * The URL a browser shows for a path of the router: the home of a locale gets the trailing
 * slash of ADR 0005 (`/fr` → `/fr/`, query and fragment kept), every other path stays as the
 * router writes it.
 */
export function withHomeSlash(url: string): string {
  const end = url.search(PATH_END);
  const path = end === -1 ? url : url.slice(0, end);
  const [root, locale, ...rest] = path.split('/');
  const home = root === '' && isLocale(locale) && rest.length === 0;
  return home ? `${path}/${url.slice(path.length)}` : url;
}
