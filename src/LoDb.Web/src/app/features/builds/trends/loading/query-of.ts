const QUERY_START = '?';
const FRAGMENT_START = '#';

/** The query of a URL as the router serialized it, `?` included, or ''. */
export function queryOf(url: string): string {
  const path = url.split(FRAGMENT_START, 1)[0];
  const start = path.indexOf(QUERY_START);
  return start < 0 ? '' : path.slice(start);
}
