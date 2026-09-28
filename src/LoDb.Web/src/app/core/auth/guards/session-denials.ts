import type { RedirectCommand, UrlTree } from '@angular/router';

/** What a session guard answers instead of the requested page. */
export interface SessionDenials {
  /** `/{locale}/account/{page}?returnUrl={requested URL}`, in the locale of the route. */
  accountPage(page: 'login' | 'verify-email'): UrlTree;
  /** The 404 of the requested URL, rendered in place, which hides what the page is. */
  notFound(): RedirectCommand;
}
