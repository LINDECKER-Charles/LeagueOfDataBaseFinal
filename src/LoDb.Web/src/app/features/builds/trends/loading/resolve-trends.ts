import { HttpErrorResponse, HttpStatusCode } from '@angular/common/http';
import { inject } from '@angular/core';
import type { ResolveFn } from '@angular/router';
import { firstValueFrom } from 'rxjs';
import type { ListTrends$Params } from '../../../../core/api/generated/fn/trends/list-trends';
import { TrendsService } from '../../../../core/api/generated/services/trends.service';
import { ApiMeta } from '../../../../core/api/meta/api-meta';
import { pageContextOf } from '../../../../core/context/page-context-of';
import { DEFAULT_LOCALE } from '../../../../core/i18n/default-locale';
import { isLocale } from '../../../../core/i18n/is-locale';
import { injectOutcomeCommand } from '../../../../core/routing/outcome/inject-outcome-command';
import { NOT_FOUND } from '../../../../core/routing/outcome/not-found';
import { PageResponse } from '../../../../core/routing/response/page-response';
import { trendsQueryOf } from '../filters/trends-query-of';
import { queryOf } from './query-of';
import type { TrendsView } from './trends-view';

// A `?version=` or `?lang=` the API refuses (400) or does not know (404): no page lives there.
const NO_PAGE: readonly number[] = [HttpStatusCode.NotFound, HttpStatusCode.BadRequest];

// The filters the URL names, and nothing for those it leaves out.
function requestOf(query: ReturnType<typeof trendsQueryOf>): ListTrends$Params {
  const { champion, mode, language, page } = query;
  return {
    ...(champion === null ? {} : { champion }),
    ...(mode === null ? {} : { mode }),
    ...(language === null ? {} : { language }),
    ...(page === 1 ? {} : { page }),
  };
}

/**
 * Resolver of the trends (`trends`): a page of public builds for the filters of the URL,
 * read anonymously, their names in the context of `?version=` and `?lang=` like any page
 * outside the catalogue. Its proxy cache lasts a minute: the ranking moves with every vote.
 */
export const resolveTrends: ResolveFn<TrendsView> = async (route, state) => {
  const meta$ = inject(ApiMeta).meta();
  const trends = inject(TrendsService);
  const answer = injectOutcomeCommand();
  const response = inject(PageResponse);
  const requested = route.paramMap.get('locale');
  const locale = isLocale(requested) ? requested : DEFAULT_LOCALE;
  const meta = await firstValueFrom(meta$);
  const sources = { locale, path: null, query: queryOf(state.url), remembered: null };
  const context = pageContextOf(sources, meta);
  const names = context === null ? {} : { version: context.version, lang: context.language };
  const request = { ...requestOf(trendsQueryOf(route.queryParamMap)), ...names };
  try {
    const page = await firstValueFrom(trends.listTrends(request));
    response.cache('transient');
    return { locale, page, modes: meta.gameModes.map((entry) => entry.mode), request };
  } catch (error) {
    if (error instanceof HttpErrorResponse && NO_PAGE.includes(error.status)) {
      return answer(NOT_FOUND, state.url);
    }
    throw error;
  }
};
