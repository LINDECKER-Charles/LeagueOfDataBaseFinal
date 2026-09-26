import type { FilterCriteria } from './filter-criteria';

/** The criteria plus the page the reader stands on. */
export interface GridCriteria extends FilterCriteria {
  /** One-based. */
  readonly page: number;
  /** PAGE_SIZE_ALL or a positive page size. */
  readonly pageSize: number;
}
