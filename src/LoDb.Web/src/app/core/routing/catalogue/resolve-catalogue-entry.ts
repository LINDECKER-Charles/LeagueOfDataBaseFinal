import { inject } from '@angular/core';
import type { ResolveFn } from '@angular/router';
import { firstValueFrom } from 'rxjs';
import type { ResourceType } from '../../api/generated/models/resource-type';
import { ApiMeta } from '../../api/meta/api-meta';
import { canonical } from '../canonical';
import { injectOutcomeCommand } from '../outcome/inject-outcome-command';
import { NOT_FOUND } from '../outcome/not-found';
import { PageResponse } from '../response/page-response';
import { cacheClassOf } from '../version/cache-class-of';
import type { CatalogueEntry } from './catalogue-entry';
import { cataloguePageOf } from './catalogue-page-of';
import type { DetailsByResource } from './details-by-resource';
import { injectDetailsFetcher } from './inject-details-fetcher';
import { factsOf } from './facts-of';
import { openCatalogue } from './open-catalogue';

/**
 * Resolver of a catalogue detail (`entry`): the rules `/api/meta` decides come first, so an
 * unknown or latest pinned version never reaches the API; then the entity, whose
 * `canonicalPath` fixes the slug or, when absent, sends to its version's list or to a 404.
 */
export function resolveCatalogueEntry<R extends ResourceType>(
  resource: R,
): ResolveFn<CatalogueEntry<DetailsByResource[R]>> {
  return async (route, state) => {
    const meta$ = inject(ApiMeta).meta();
    const fetchDetails = injectDetailsFetcher();
    const answer = injectOutcomeCommand();
    const response = inject(PageResponse);
    const page = cataloguePageOf(route, state, resource);
    const meta = await firstValueFrom(meta$);
    const context = openCatalogue(page, meta);
    if ('kind' in context) {
      return answer(context, state.url);
    }
    const { version, language } = context;
    const details = await fetchDetails(resource, { version, lang: language, id: page.entry ?? '' });
    const outcome = canonical(page, factsOf(meta, details?.canonicalPath ?? null));
    if (outcome !== null || details === null) {
      return answer(outcome ?? NOT_FOUND, state.url);
    }
    response.cache(cacheClassOf(version, meta));
    return { context, details };
  };
}
