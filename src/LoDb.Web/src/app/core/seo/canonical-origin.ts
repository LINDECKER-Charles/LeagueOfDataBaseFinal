import { DOCUMENT, InjectionToken, PLATFORM_ID, inject } from '@angular/core';
import { isPlatformBrowser } from '@angular/common';
import { injectPageOrigin } from '../http/inject-page-origin';
import { SITE_IDENTITY } from './site-identity';

/**
 * The origin every canonical, alternate and Open Graph URL is written with, without a
 * trailing slash. nginx folds `www.` and `.fr` into the canonical host before the SSR sees a
 * request, so the request's origin is the canonical one; the browser keeps the origin it was
 * served from. A render without a request (prerender) states production's.
 */
export const CANONICAL_ORIGIN = new InjectionToken<string>('CANONICAL_ORIGIN', {
  providedIn: 'root',
  factory: () => {
    const requested = injectPageOrigin();
    if (requested !== null) {
      return requested;
    }
    return isPlatformBrowser(inject(PLATFORM_ID))
      ? inject(DOCUMENT).location.origin
      : SITE_IDENTITY.productionOrigin;
  },
});
