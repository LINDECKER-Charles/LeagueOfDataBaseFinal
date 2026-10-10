import { CHAMPION_BASE_STATS } from './champion-base-stats';

/**
 * URL parameters of the champion list's facets: its schema (`championFacets`) keys its
 * filters on them, and the SSR server answers a list URL carrying any other parameter with a
 * redirect to the URL without it. A facet keyed elsewhere would lose its filter on reload.
 */
export const CHAMPION_FACET_KEYS = {
  role: 'role',
  resource: 'resource',
  range: 'range',
  ratings: ['difficulty', 'attack', 'defense', 'magic'],
  baseStats: CHAMPION_BASE_STATS.map(({ key }) => key),
} as const;
