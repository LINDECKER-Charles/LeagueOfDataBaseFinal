import { type Observable, concat, of, switchMap, timer } from 'rxjs';
import type { ListOutcome } from './list-outcome';

function retryDelayOf<L>(outcome: ListOutcome<L>): number | null {
  return outcome.kind === 'failed' ? null : outcome.retryAfterMs;
}

/**
 * A list call, then ONE more once the delay its answer asked for has passed, never more:
 * placeholders of a cold version get one chance to fill in, without a polling loop. The
 * second answer is final, whatever it asks for.
 */
export function withOneRetry<L>(
  fetch: () => Observable<ListOutcome<L>>,
): Observable<ListOutcome<L>> {
  return fetch().pipe(
    switchMap((first) => {
      const delay = retryDelayOf(first);
      return delay === null ? of(first) : concat(of(first), timer(delay).pipe(switchMap(fetch)));
    }),
  );
}
