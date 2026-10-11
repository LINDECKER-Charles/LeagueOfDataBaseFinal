import type { ActivatedRouteSnapshot, Params, Router } from '@angular/router';

// What the link of an account e-mail adds to its page's query (email-link.ts).
const LINK_PARAMS: readonly string[] = ['user', 'token'];

function withoutLink(query: Params): Params {
  return Object.fromEntries(Object.entries(query).filter(([key]) => !LINK_PARAMS.includes(key)));
}

/**
 * Takes the token of an e-mail link out of the address bar and out of the history entry, once
 * the page has read it: the page, `page`, stays with the rest of its query, without the token
 * in its path, query or fragment. A replace, so going back never finds the token again; done
 * after the first render, once the navigation that opened the page is over.
 */
export function forgetLinkToken(
  router: Router,
  page: string,
  route: ActivatedRouteSnapshot,
): Promise<boolean> {
  const clean = router.createUrlTree([page], { queryParams: withoutLink(route.queryParams) });
  return router.navigateByUrl(clean, { replaceUrl: true });
}
