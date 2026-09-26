import { DOCUMENT, REQUEST, inject } from '@angular/core';
import type { RedirectFunction } from '@angular/router';
import { negotiateLocale } from './negotiate-locale';
import { parseAcceptLanguage } from './parse-accept-language';

const ACCEPT_LANGUAGE = 'accept-language';

/**
 * Target of `/` inside the application: the shell build of the apps has no server to answer
 * it. The SSR server answers `/` itself with a 302 (src/server.ts), so this only runs there
 * when another server hosts the handler; it then reads the same header.
 *
 * Without the trailing slash of the server's `/{locale}/`: the router reads `/fr/` as `fr`
 * followed by an empty segment, which no route matches. Only the first navigation drops it,
 * through `Location`.
 */
export const redirectToPreferredLocale: RedirectFunction = () => {
  const request = inject(REQUEST, { optional: true });
  const tags = request
    ? parseAcceptLanguage(request.headers.get(ACCEPT_LANGUAGE))
    : (inject(DOCUMENT).defaultView?.navigator.languages ?? []);
  return `/${negotiateLocale(tags)}`;
};
