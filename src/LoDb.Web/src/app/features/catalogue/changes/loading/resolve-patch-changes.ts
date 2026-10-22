import { HttpErrorResponse, HttpStatusCode } from '@angular/common/http';
import { inject } from '@angular/core';
import type { ResolveFn } from '@angular/router';
import { firstValueFrom, type Observable } from 'rxjs';
import type { PatchChanges } from '../../../../core/api/generated/models/patch-changes';
import { CatalogService } from '../../../../core/api/generated/services/catalog.service';
import { ApiMeta } from '../../../../core/api/meta/api-meta';
import type { CatalogueEntry } from '../../../../core/routing/catalogue/catalogue-entry';
import { cataloguePageOf } from '../../../../core/routing/catalogue/catalogue-page-of';
import { openCatalogue } from '../../../../core/routing/catalogue/open-catalogue';
import { injectOutcomeCommand } from '../../../../core/routing/outcome/inject-outcome-command';
import { NOT_FOUND } from '../../../../core/routing/outcome/not-found';
import { PATCH_CHANGES_PATH } from '../../../../core/routing/patch-changes-path';
import { PageResponse } from '../../../../core/routing/response/page-response';
import { cacheClassOf } from '../../../../core/routing/version/cache-class-of';

// A 404 says the version has no page of changes (the first one the index covers, one outside
// it, one newer than the latest); any other failure belongs to the navigation error handler.
async function orAbsent<T>(answer: Observable<T>): Promise<T | null> {
  try {
    return await firstValueFrom(answer);
  } catch (error) {
    if (error instanceof HttpErrorResponse && error.status === HttpStatusCode.NotFound) {
      return null;
    }
    throw error;
  }
}

/**
 * Resolver of the page of a patch's changes (`changes`): the URL rules of a list first (an
 * unknown version is a 404, the latest pinned a 301 to `/{locale}/changes`, a known
 * `?version=` a 301 into the path), then the API's changes of the version, a real 404 when it
 * has none. An older patch's page never changes again (`archived`); the latest's, `latest`.
 */
export const resolvePatchChanges: ResolveFn<CatalogueEntry<PatchChanges>> = async (
  route,
  state,
) => {
  const meta$ = inject(ApiMeta).meta();
  const catalog = inject(CatalogService);
  const answer = injectOutcomeCommand();
  const response = inject(PageResponse);
  const meta = await firstValueFrom(meta$);
  const context = openCatalogue(cataloguePageOf(route, state, PATCH_CHANGES_PATH), meta);
  if ('kind' in context) {
    return answer(context, state.url);
  }
  const request = { version: context.version, lang: context.language };
  const changes = await orAbsent(catalog.getPatchChanges(request));
  if (changes === null) {
    return answer(NOT_FOUND, state.url);
  }
  response.cache(cacheClassOf(context.version, meta));
  return { context, details: changes };
};
