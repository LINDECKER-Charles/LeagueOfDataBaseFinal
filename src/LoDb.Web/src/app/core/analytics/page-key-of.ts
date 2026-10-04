import { QueryString } from '../routing/url/query-string';

const QUERY_START = '?';
const KEY_SEPARATOR = '\n';

// The parameters that change the page itself; the others (filters, pagination) only
// rearrange it, and a view is counted per page, not per keystroke in a filter.
const PAGE_PARAMS = ['lang', 'version'] as const;

/**
 * What identifies a counted page in a router URL (without fragment): its path, its language
 * and its version. Two URLs with the same key are one view.
 */
export function pageKeyOf(url: string): string {
  const start = url.indexOf(QUERY_START);
  const path = start === -1 ? url : url.slice(0, start);
  const query = QueryString.parse(start === -1 ? '' : url.slice(start));
  return [path, ...PAGE_PARAMS.map((name) => query.get(name) ?? '')].join(KEY_SEPARATOR);
}
