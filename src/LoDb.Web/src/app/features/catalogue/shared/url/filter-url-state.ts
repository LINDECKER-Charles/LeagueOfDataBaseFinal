import type { FacetState } from '../facets/model/facet-state';

/** The part of a list's state its URL carries, so a filtered list is a shareable link. */
export interface FilterUrlState {
  readonly query: string;
  readonly facets: FacetState;
  /** One-based. */
  readonly page: number;
  /** PAGE_SIZE_ALL or a positive page size. */
  readonly size: number;
}
