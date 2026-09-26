import { inject } from '@angular/core';
import type { ResolveFn } from '@angular/router';
import { firstValueFrom } from 'rxjs';
import type { ResourceType } from '../../api/generated/models/resource-type';
import { ApiMeta } from '../../api/meta/api-meta';
import type { PageContext } from '../../context/page-context';
import { injectOutcomeCommand } from '../outcome/inject-outcome-command';
import { PageResponse } from '../response/page-response';
import { cacheClassOf } from '../version/cache-class-of';
import { cataloguePageOf } from './catalogue-page-of';
import { openCatalogue } from './open-catalogue';

/**
 * Resolver of a catalogue list (`context`): applies the canonical rules of the URL, then
 * gives the page its version and language. The list itself is the page's to fetch (L3.5).
 */
export function resolveCatalogueContext(resource: ResourceType): ResolveFn<PageContext> {
  return async (route, state) => {
    const meta$ = inject(ApiMeta).meta();
    const answer = injectOutcomeCommand();
    const response = inject(PageResponse);
    const meta = await firstValueFrom(meta$);
    const opened = openCatalogue(cataloguePageOf(route, state, resource), meta);
    if ('kind' in opened) {
      return answer(opened, state.url);
    }
    response.cache(cacheClassOf(opened.version, meta));
    return opened;
  };
}
