/**
 * URL parameters of the summoner spell list's facets: its schema (`summonerFacetsOf`) keys
 * its filters on them, and the SSR server answers a list URL carrying any other parameter
 * with a redirect to the URL without it.
 */
export const SUMMONER_FACET_KEYS = {
  mode: 'mode',
  edition: 'edition',
  level: 'level',
  cooldown: 'cooldown',
} as const;
