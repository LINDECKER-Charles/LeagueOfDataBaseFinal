import { HttpErrorResponse, HttpStatusCode } from '@angular/common/http';
import { inject } from '@angular/core';
import type { ResolveFn } from '@angular/router';
import { firstValueFrom } from 'rxjs';
import type { PublicProfile } from '../../../core/api/generated/models/public-profile';
import { ProfileService } from '../../../core/api/generated/services/profile.service';
import { ApiMeta } from '../../../core/api/meta/api-meta';
import { pageContextOf } from '../../../core/context/page-context-of';
import { DEFAULT_LOCALE } from '../../../core/i18n/default-locale';
import { isLocale } from '../../../core/i18n/is-locale';
import { injectOutcomeCommand } from '../../../core/routing/outcome/inject-outcome-command';
import { NOT_FOUND } from '../../../core/routing/outcome/not-found';
import { PageResponse } from '../../../core/routing/response/page-response';
import { queryOf } from './query-of';

// The API answers one 404 for a name no account holds, a private profile and a banned owner,
// and a 400 for a version or a language it refuses: no card lives at either URL.
const NO_CARD: readonly number[] = [HttpStatusCode.NotFound, HttpStatusCode.BadRequest];

/**
 * Resolver of a public profile (`profile`): its card, read anonymously in the context of
 * `?version=` and `?lang=` like any page outside the catalogue (the server reads no cookie);
 * a version the owner pinned wins over them on the API's side. Its proxy cache lasts a
 * minute only: the owner may make the card private at any time.
 */
export const resolvePublicProfile: ResolveFn<PublicProfile> = async (route, state) => {
  const meta$ = inject(ApiMeta).meta();
  const profiles = inject(ProfileService);
  const answer = injectOutcomeCommand();
  const response = inject(PageResponse);
  const requested = route.paramMap.get('locale');
  const locale = isLocale(requested) ? requested : DEFAULT_LOCALE;
  const username = route.paramMap.get('username') ?? '';
  const sources = { locale, path: null, query: queryOf(state.url), remembered: null };
  const context = pageContextOf(sources, await firstValueFrom(meta$));
  const query = context === null ? {} : { version: context.version, lang: context.language };
  try {
    const profile = await firstValueFrom(profiles.getPublicProfile({ username, ...query }));
    response.cache('transient');
    return profile;
  } catch (error) {
    if (error instanceof HttpErrorResponse && NO_CARD.includes(error.status)) {
      return answer(NOT_FOUND, state.url);
    }
    throw error;
  }
};
