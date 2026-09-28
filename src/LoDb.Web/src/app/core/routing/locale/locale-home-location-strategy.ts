import { PathLocationStrategy } from '@angular/common';
import { Injectable } from '@angular/core';
import { withHomeSlash } from './with-home-slash';

/**
 * Writes the home of a locale as `/{locale}/`, its URL in ADR 0005, although the router knows
 * it as `/{locale}`: `Location` drops a trailing slash when it reads the address, and the
 * first navigation would otherwise replace the `/fr/` the server answered with `/fr`. Links
 * to the home carry the slash too. Angular's own strategies add or drop it on every path.
 */
@Injectable({ providedIn: 'root' })
export class LocaleHomeLocationStrategy extends PathLocationStrategy {
  override prepareExternalUrl(internal: string): string {
    return super.prepareExternalUrl(withHomeSlash(internal));
  }
}
