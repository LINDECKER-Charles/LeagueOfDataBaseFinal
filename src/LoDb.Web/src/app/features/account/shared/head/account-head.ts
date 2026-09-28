import { Injectable, signal } from '@angular/core';
import type { AccountTitle } from './account-title';

/**
 * The title a view of an account page gives its head over the route's own: the summoner's
 * name of the editor and of the preview, the "check your inbox" of a reset request sent.
 * Provided by AccountPage, which writes the head; null leaves the route's title.
 */
@Injectable()
export class AccountHead {
  readonly override = signal<AccountTitle | null>(null);
}
