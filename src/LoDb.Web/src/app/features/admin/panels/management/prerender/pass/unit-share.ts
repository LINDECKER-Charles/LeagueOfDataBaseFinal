import type { PrerenderUnitCounts } from '../../../../../../core/api/generated/models/prerender-unit-counts';

/**
 * The share of the units of a pass already completed, from 0 to 1: its progress. The units
 * are all known when the pass opens, unlike its pages, listed as it reaches each unit.
 */
export function unitShare(units: PrerenderUnitCounts): number {
  return units.total > 0 ? units.completed / units.total : 0;
}
