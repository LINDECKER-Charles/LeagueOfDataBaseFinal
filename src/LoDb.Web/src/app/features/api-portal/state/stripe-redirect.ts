import { DOCUMENT, Injectable, inject } from '@angular/core';

/**
 * Leaves the portal for Stripe's hosted page the API opened. Only an `https:` address is
 * followed: whatever else the answer holds, the buyer stays on the portal. A spec replaces it
 * to see where the page would have gone.
 */
@Injectable({ providedIn: 'root' })
export class PortalStripeRedirect {
  private readonly document = inject(DOCUMENT);

  /** Follows `url`; false when it is no secure address and the page stays. */
  go(url: string): boolean {
    if (!URL.canParse(url) || new URL(url).protocol !== 'https:') {
      return false;
    }
    this.document.location.assign(url);
    return true;
  }
}
