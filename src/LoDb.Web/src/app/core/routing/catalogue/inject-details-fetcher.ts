import { HttpErrorResponse, HttpStatusCode } from '@angular/common/http';
import { inject } from '@angular/core';
import { firstValueFrom, type Observable } from 'rxjs';
import type { ResourceType } from '../../api/generated/models/resource-type';
import { CatalogService } from '../../api/generated/services/catalog.service';
import type { DetailsByResource } from './details-by-resource';

/** Version, language and entry segment (`1036-long-sword`) of a detail. */
interface DetailsRequest {
  readonly version: string;
  readonly lang: string;
  readonly id: string;
}

/** Fetches a detail, null when the API holds no such entity. */
type DetailsFetcher = <R extends ResourceType>(
  resource: R,
  request: DetailsRequest,
) => Promise<DetailsByResource[R] | null>;

type Fetchers = {
  readonly [R in ResourceType]: (request: DetailsRequest) => Observable<DetailsByResource[R]>;
};

// A 404 says the entity is not in this version and language; any other failure (the API
// down, a cold version not ingested yet) belongs to the navigation error handler.
async function orAbsent<T>(details: Observable<T>): Promise<T | null> {
  try {
    return await firstValueFrom(details);
  } catch (error) {
    if (error instanceof HttpErrorResponse && error.status === HttpStatusCode.NotFound) {
      return null;
    }
    throw error;
  }
}

/** Builds the detail fetcher, injected up front since resolvers call it after an await. */
export function injectDetailsFetcher(): DetailsFetcher {
  const catalog = inject(CatalogService);
  const fetchers: Fetchers = {
    champions: (request) => catalog.getChampion(request),
    items: (request) => catalog.getItem(request),
    runes: (request) => catalog.getRuneTree(request),
    summoners: (request) => catalog.getSummoner(request),
  };
  return (resource, request) => orAbsent(fetchers[resource](request));
}
