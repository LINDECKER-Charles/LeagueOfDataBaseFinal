import { isPlatformBrowser } from '@angular/common';
import { HttpErrorResponse, HttpStatusCode } from '@angular/common/http';
import { PLATFORM_ID, inject } from '@angular/core';
import {
  type ActivatedRouteSnapshot,
  RedirectCommand,
  type ResolveFn,
  Router,
} from '@angular/router';
import { TranslocoService } from '@jsverse/transloco';
import { DEFAULT_LOCALE } from '../../../../core/i18n/default-locale';
import { isLocale } from '../../../../core/i18n/is-locale';
import type { Locale } from '../../../../core/i18n/locales';
import { localePath } from '../../../../core/layout/shell/locale-path';
import { ToastService } from '../../../../core/layout/toast/toast-service';
import { failureOf } from '../../../../core/routing/failure/failure-of';
import { injectOutcomeCommand } from '../../../../core/routing/outcome/inject-outcome-command';
import { NOT_FOUND } from '../../../../core/routing/outcome/not-found';
import type { EditorEntry } from '../editor-entry';
import { EditorEntries } from './editor-entries';
import type { EntryRequest } from './entry-request';

// A build id as the API issues them: a positive integer.
const BUILD_ID = /^[1-9]\d{0,14}$/;

function localeOf(route: ActivatedRouteSnapshot): Locale {
  return route.pathFromRoot.map((step) => step.params['locale']).find(isLocale) ?? DEFAULT_LOCALE;
}

function requestOf(route: ActivatedRouteSnapshot, url: string): EntryRequest | null {
  const view = route.data['view'] as EntryRequest['view'];
  const raw = route.paramMap.get('id');
  const id = raw !== null && BUILD_ID.test(raw) ? Number(raw) : null;
  if (view !== 'create' && id === null) {
    return null;
  }
  return { view, id, locale: localeOf(route), url, to: route.queryParamMap.get('to') };
}

function isUnavailable(error: unknown): boolean {
  return error instanceof HttpErrorResponse && error.status === HttpStatusCode.ServiceUnavailable;
}

/**
 * Back to the editor of the build when the catalogue of the patch it was imported to is out
 * of reach: the author keeps their build, told to try again shortly.
 */
function injectImportFallback(): (request: EntryRequest) => RedirectCommand {
  const router = inject(Router);
  const toasts = inject(ToastService);
  const transloco = inject(TranslocoService);
  return (request) => {
    toasts.show('error', transloco.translate('build.error.catalog_unavailable'));
    const edit = localePath(request.locale, `account/builds/${request.id}/edit`);
    return new RedirectCommand(router.parseUrl(edit), { replaceUrl: true });
  };
}

/**
 * Resolver of the editor (`entry`), in the browser only: the account's builds never render
 * on a server, which gets nothing to resolve. An id that is none, or a build this account
 * does not own, is the 404 of the URL.
 */
export const resolveEditorEntry: ResolveFn<EditorEntry | null> = async (route, state) => {
  if (!isPlatformBrowser(inject(PLATFORM_ID))) {
    return null;
  }
  const entries = inject(EditorEntries);
  const answer = injectOutcomeCommand();
  const fallBack = injectImportFallback();
  const request = requestOf(route, state.url);
  if (request === null) {
    return answer(NOT_FOUND, state.url);
  }
  try {
    return await entries.load(request);
  } catch (error) {
    if (error instanceof HttpErrorResponse && error.status === HttpStatusCode.NotFound) {
      return answer(NOT_FOUND, state.url);
    }
    if (request.view === 'import' && isUnavailable(error)) {
      return fallBack(request);
    }
    return answer(failureOf(error), state.url);
  }
};
