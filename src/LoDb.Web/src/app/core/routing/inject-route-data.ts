import { computed, inject, type Signal } from '@angular/core';
import { toSignal } from '@angular/core/rxjs-interop';
import { ActivatedRoute } from '@angular/router';

/**
 * A value the route of the current component resolved or declared (`context`, `entry`,
 * `outcome`...), as a signal that follows the route when the component is reused. Pages read
 * their data through it: component input binding stays off, because it binds the query as
 * well, and a crafted query would then fill any input the route itself leaves unset.
 */
export function injectRouteData<T>(key: string): Signal<T> {
  const data = toSignal(inject(ActivatedRoute).data, { requireSync: true });
  return computed(() => data()[key] as T);
}
