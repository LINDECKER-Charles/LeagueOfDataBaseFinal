import { inject } from '@angular/core';
import { Router, type ResolveFn } from '@angular/router';
import { PageResponse } from '../response/page-response';
import { NOT_FOUND } from './not-found';
import type { PageOutcome } from './page-outcome';
import { pendingOutcome } from './pending-outcome';

/**
 * The outcome the error page shows, answered in the server response on the way: the one
 * the navigation carries, or a 404 when the error route matched a URL no route knows.
 */
export const resolveOutcome: ResolveFn<PageOutcome> = () => {
  const outcome = pendingOutcome(inject(Router)) ?? NOT_FOUND;
  inject(PageResponse).answer(outcome);
  return outcome;
};
