import { isPlatformBrowser } from '@angular/common';
import { PLATFORM_ID, inject } from '@angular/core';
import { RedirectCommand, Router } from '@angular/router';
import { NavigationOutcome } from './navigation-outcome';
import type { PageOutcome } from './page-outcome';

/** Turns an outcome into what a guard, a resolver or the error handler returns. */
export type OutcomeCommand = (outcome: PageOutcome, url: string) => RedirectCommand;

/**
 * Builds the answer of an outcome instead of the page at `url`. The server renders the
 * outcome in place, at the same URL, so that `PageResponse` writes its real status and
 * `Location`; the browser follows a redirect as a navigation that replaces the stale entry
 * of the history. Injected up front, since a resolver answers after awaiting the API.
 */
export function injectOutcomeCommand(): OutcomeCommand {
  const router = inject(Router);
  const inBrowser = isPlatformBrowser(inject(PLATFORM_ID));
  return (outcome, url) =>
    outcome.kind === 'redirect' && inBrowser
      ? new RedirectCommand(router.parseUrl(outcome.location), { replaceUrl: true })
      : new RedirectCommand(router.parseUrl(url), { info: new NavigationOutcome(outcome) });
}
