const QUERY_START = '?';
const FRAGMENT_START = '#';

/** The query string of a root-relative URL, `?` included, without its fragment; or ''. */
export function searchOf(url: string): string {
  const beforeHash = url.split(FRAGMENT_START, 1)[0];
  const start = beforeHash.indexOf(QUERY_START);
  return start < 0 ? '' : beforeHash.slice(start);
}
