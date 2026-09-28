import { HttpClient } from '@angular/common/http';
import { computed, inject, linkedSignal } from '@angular/core';
import { rxResource } from '@angular/core/rxjs-interop';
import { map, timeout } from 'rxjs';
import { API_BASE_URL } from '../../../core/api/api-base-url';
import type { AdminCall } from '../shared/http/admin-call';
import { ADMIN_TIMEOUT } from '../shared/http/admin-timeout';
import type { Panel } from './panel';
import { panelFailureOf } from './panel-failure';

/**
 * Loads the data of a panel through an operation of the generated client, with the
 * parameters `params` reads (the query of the page, usually). A load that takes longer
 * than `ADMIN_TIMEOUT` fails as `timeout`; `reload` tries again.
 */
export function injectPanel<P, T>(call: AdminCall<P, T>, params: () => P): Panel<T> {
  const http = inject(HttpClient);
  const rootUrl = inject(API_BASE_URL);
  const wait = inject(ADMIN_TIMEOUT);
  const resource = rxResource({
    params,
    stream: ({ params: query }) =>
      call(http, rootUrl, query).pipe(
        timeout({ first: wait }),
        map((response) => response.body),
      ),
  });
  const value = linkedSignal<{ current: T | undefined; busy: boolean }, T | undefined>({
    source: () => ({
      current: resource.hasValue() ? resource.value() : undefined,
      busy: resource.isLoading(),
    }),
    computation: ({ current, busy }, previous) => current ?? (busy ? previous?.value : undefined),
  });
  return {
    value,
    busy: resource.isLoading,
    failure: computed(() =>
      resource.status() === 'error' ? panelFailureOf(resource.error()) : null,
    ),
    reload: () => resource.reload(),
  };
}
