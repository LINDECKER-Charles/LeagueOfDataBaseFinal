import { QueryString } from '../../../routing/url/query-string';

const QUERY_START = '?';
const PAIR_SEPARATOR = '&';

// The only parameters sent, each with the shape it must have: a Data Dragon language and a
// version. Anything else (a search, the facets, the paging, a crafted value) is dropped.
const KEPT: readonly (readonly [name: string, shape: RegExp])[] = [
  ['lang', /^[a-z]{2}_[A-Z]{2}$/],
  ['version', /^[\w.]{1,32}$/],
];

/**
 * The query of a public path as the audience receives it: the language and the version a
 * page shows, in a fixed order, and nothing else. Empty when neither is there.
 */
export function audienceQueryOf(search: string): string {
  const query = QueryString.parse(search);
  const kept = KEPT.flatMap(([name, shape]) => {
    const value = query.get(name);
    return value !== null && shape.test(value) ? [`${name}=${encodeURIComponent(value)}`] : [];
  });
  return kept.length === 0 ? '' : QUERY_START + kept.join(PAIR_SEPARATOR);
}
