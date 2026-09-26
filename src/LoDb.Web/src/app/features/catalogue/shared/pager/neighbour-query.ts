import type { PageContext } from '../../../../core/context/page-context';

/** Where a detail page stands: its context and the key of its entry in the list. */
export interface NeighbourQuery {
  readonly context: PageContext;
  readonly key: string;
}
