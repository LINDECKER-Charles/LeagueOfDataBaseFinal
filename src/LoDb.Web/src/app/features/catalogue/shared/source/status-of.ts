import type { ListOutcome } from '../data/list-outcome';
import type { ListStatus } from './list-status';

/** The status a list page shows from the most complete answer it has, if any. */
export function statusOf<L>(outcome: ListOutcome<L> | undefined): ListStatus {
  if (outcome === undefined) {
    return 'loading';
  }
  switch (outcome.kind) {
    case 'list':
      return 'ready';
    case 'pending':
      return 'pending';
    case 'failed':
      return 'failed';
  }
}
