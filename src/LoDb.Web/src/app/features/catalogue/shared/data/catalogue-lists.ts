import { HttpErrorResponse, type HttpHeaders, HttpStatusCode } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { type Observable, catchError, map, of } from 'rxjs';
import type { ResourceType } from '../../../../core/api/generated/models/resource-type';
import { CatalogService } from '../../../../core/api/generated/services/catalog.service';
import type { StrictHttpResponse } from '../../../../core/api/generated/strict-http-response';
import { retryAfterOf } from '../../../../core/http/retry-after-of';
import type { ListByResource } from './list-by-resource';
import type { ListOutcome } from './list-outcome';
import type { ListRequest } from './list-request';

type Fetchers = {
  readonly [R in ResourceType]: (
    request: ListRequest,
  ) => Observable<StrictHttpResponse<ListByResource[R]>>;
};

function delayOf(headers: HttpHeaders): number | null {
  return retryAfterOf(headers.get('Retry-After'), Date.now());
}

function failureOf<L>(error: unknown): Observable<ListOutcome<L>> {
  const isPending =
    error instanceof HttpErrorResponse && error.status === HttpStatusCode.ServiceUnavailable;
  return of(
    isPending ? { kind: 'pending', retryAfterMs: delayOf(error.headers) } : { kind: 'failed' },
  );
}

/**
 * The catalogue lists, read with their headers: a list whose images are still placeholders,
 * like the 503 of a version not ingested yet, carries the `Retry-After` of its one retry.
 * Failures become outcomes, so a list page renders whatever happens.
 */
@Injectable({ providedIn: 'root' })
export class CatalogueLists {
  private readonly catalog = inject(CatalogService);
  private readonly fetchers: Fetchers = {
    champions: (request) => this.catalog.listChampions$Response(request),
    items: (request) => this.catalog.listItems$Response(request),
    runes: (request) => this.catalog.listRunes$Response(request),
    summoners: (request) => this.catalog.listSummoners$Response(request),
  };

  fetch<R extends ResourceType>(
    resource: R,
    request: ListRequest,
  ): Observable<ListOutcome<ListByResource[R]>> {
    return this.fetchers[resource](request).pipe(
      map((response): ListOutcome<ListByResource[R]> => ({
        kind: 'list',
        list: response.body,
        retryAfterMs: delayOf(response.headers),
      })),
      catchError((error: unknown) => failureOf<ListByResource[R]>(error)),
    );
  }
}
