import { inject } from '@angular/core';
import type { ResolveFn } from '@angular/router';
import { firstValueFrom } from 'rxjs';
import { ApiMeta } from '../../../core/api/meta/api-meta';
import { pageContextOf } from '../../../core/context/page-context-of';
import { DEFAULT_LOCALE } from '../../../core/i18n/default-locale';
import { isLocale } from '../../../core/i18n/is-locale';
import { PageResponse } from '../../../core/routing/response/page-response';
import { cacheClassOf } from '../../../core/routing/version/cache-class-of';
import type { HomeData } from '../data/home-data';
import type { Previews } from '../data/previews';
import { injectPreviewsFetcher } from './inject-previews-fetcher';
import { linkMakerOf } from './link-maker-of';
import { queryOf } from './query-of';
import { sectionsOf } from './sections-of';

const NO_PREVIEWS: Previews = { champions: null, items: null, runes: null, summoners: null };

/**
 * Resolver of the home (`home`): its context, read from `?version=` and `?lang=` like any
 * page outside the catalogue (the server reads no cookie), then the four previews in that
 * context. An older version is cached as long as a pinned catalogue page, unless a preview
 * still waits for its images: that page stays out of shared caches, and the browser reads
 * it again once (HomePage). `/api/meta` out of reach fails the navigation (503); one list
 * out of reach only empties its section.
 */
export const resolveHome: ResolveFn<HomeData> = async (route, state) => {
  const meta$ = inject(ApiMeta).meta();
  const fetchPreviews = injectPreviewsFetcher();
  const response = inject(PageResponse);
  const requested = route.paramMap.get('locale');
  const locale = isLocale(requested) ? requested : DEFAULT_LOCALE;
  const meta = await firstValueFrom(meta$);
  const sources = { locale, path: null, query: queryOf(state.url), remembered: null };
  const context = pageContextOf(sources, meta);
  if (context === null) {
    const link = (path: string) => ({ path: `/${locale}/${path}`, query: {} });
    return { context: null, sections: sectionsOf(NO_PREVIEWS, link), retryAfterMs: null };
  }
  const { previews, retryAfterMs } = await fetchPreviews(context);
  response.cache(retryAfterMs === null ? cacheClassOf(context.version, meta) : 'transient');
  const sections = sectionsOf(previews, linkMakerOf(context, meta));
  return { context, sections, retryAfterMs };
};
