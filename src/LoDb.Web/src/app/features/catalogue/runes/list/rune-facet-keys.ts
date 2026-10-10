/**
 * URL parameters of the rune list's facets: its schema (`runeFacetsOf`) keys its filters on
 * them, and the SSR server answers a list URL carrying any other parameter with a redirect
 * to the URL without it.
 */
export const RUNE_FACET_KEYS = {
  path: 'path',
  slot: 'slot',
} as const;
