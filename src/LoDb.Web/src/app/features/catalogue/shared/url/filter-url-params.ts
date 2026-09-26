/**
 * The URL parameters of a list (ADR 0005, kept from the current site): `q`, `page`, `size`,
 * then one per facet under its key, and `<key>_all=1` for a match-all choice. Values of a
 * choice join with `,`, bounds of a range with `-`, a flag reads `1`, the whole list `all`.
 */
export const FILTER_URL_PARAMS = {
  query: 'q',
  page: 'page',
  size: 'size',
  matchAllSuffix: '_all',
  listSeparator: ',',
  rangeSeparator: '-',
  flag: '1',
  allSizes: 'all',
} as const;
