import type { ResourceStatus } from '@angular/core';

/** Where a picker list stands, the three states its section draws. */
export type CatalogStatus = 'loading' | 'error' | 'ready';

/** The status of a picker list, from its resource's: a reload is still a load. */
export function catalogStatus(status: ResourceStatus): CatalogStatus {
  switch (status) {
    case 'error':
      return 'error';
    case 'resolved':
    case 'local':
      return 'ready';
    default:
      return 'loading';
  }
}
