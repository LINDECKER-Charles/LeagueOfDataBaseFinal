import { HttpErrorResponse, HttpStatusCode } from '@angular/common/http';
import { inject } from '@angular/core';
import type { ResolveFn } from '@angular/router';
import { firstValueFrom } from 'rxjs';
import { BuildsService } from '../../../../core/api/generated/services/builds.service';
import { injectLocaleSwitch } from '../../../../core/i18n/inject-locale-switch';
import { injectOutcomeCommand } from '../../../../core/routing/outcome/inject-outcome-command';
import { NOT_FOUND } from '../../../../core/routing/outcome/not-found';
import { PageResponse } from '../../../../core/routing/response/page-response';
import { localeOfLanguage } from '../../shared/language/locale-of-language';
import type { SharedBuildPage } from './shared-build-page';

// A token no build holds is a 404, a malformed one too (the API checks `[a-f0-9]{24}`); so is
// a `?version=` or a `?lang=` the API refuses (400).
const NO_BUILD: readonly number[] = [HttpStatusCode.NotFound, HttpStatusCode.BadRequest];

/**
 * Resolver of a shared build (`shared`): the build read anonymously, public or private, on
 * the patch it is pinned to, then its page switched to the build's own language. `?version=`
 * names the version the visitor browses, `?lang=` the language of the names; both pass to
 * the API as they are. Its proxy cache lasts a minute: the owner may edit, hide or delete it.
 */
export const resolveSharedBuild: ResolveFn<SharedBuildPage> = async (route, state) => {
  const builds = inject(BuildsService);
  const answer = injectOutcomeCommand();
  const response = inject(PageResponse);
  const activate = injectLocaleSwitch();
  const token = route.paramMap.get('token') ?? '';
  const version = route.queryParamMap.get('version') ?? undefined;
  const lang = route.queryParamMap.get('lang') ?? undefined;
  try {
    const build = await firstValueFrom(builds.getSharedBuild({ token, version, lang }));
    const locale = localeOfLanguage(build.language);
    await activate(locale);
    response.cache('transient');
    return { build, locale };
  } catch (error) {
    if (error instanceof HttpErrorResponse && NO_BUILD.includes(error.status)) {
      return answer(NOT_FOUND, state.url);
    }
    throw error;
  }
};
