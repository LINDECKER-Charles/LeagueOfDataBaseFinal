import { HttpStatusCode } from '@angular/common/http';
import { ErrorHandler, inject } from '@angular/core';
import { Router, type NavigationError, type RedirectCommand } from '@angular/router';
import { injectOutcomeCommand } from '../outcome/inject-outcome-command';
import { pendingOutcome } from '../outcome/pending-outcome';
import { failureOf } from './failure-of';

/**
 * Handler of `withNavigationErrorHandler`: a guard, a resolver or a lazy chunk that fails
 * renders the 500 or 503 page at the requested URL, with its real status, rather than a
 * blank page in the browser or the bare 404 the SSR server answers an unfinished
 * navigation with. A 500 is logged; a 503 is the API's to report. An error while an
 * outcome renders is left to the engine, so that a broken error page cannot loop.
 */
export function handleNavigationError(error: NavigationError): RedirectCommand | undefined {
  if (pendingOutcome(inject(Router)) !== null) {
    return undefined;
  }
  const failure = failureOf(error.error);
  if (failure.status === HttpStatusCode.InternalServerError) {
    inject(ErrorHandler).handleError(error.error);
  }
  return injectOutcomeCommand()(failure, error.url);
}
