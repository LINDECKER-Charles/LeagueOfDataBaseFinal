import { ChangeDetectionStrategy, Component, inject } from '@angular/core';
import { RouterLink } from '@angular/router';
import { provideTranslocoScope, TranslocoPipe } from '@jsverse/transloco';
import { PageDirection } from '../../../core/layout/direction/page-direction';
import { localePath } from '../../../core/layout/shell/locale-path';
import { injectRouteData } from '../../../core/routing/inject-route-data';
import { Button } from '../../../ui/controls/button';
import { Frame } from '../../../ui/surfaces/frame';
import { applyDonateHead } from '../apply-donate-head';

/** Where Stripe sends the donor back: the payment went through, or they left it. */
export type DonationOutcome = 'success' | 'cancel';

/**
 * `donate/success` and `donate/cancel`, the pages Stripe returns to. Out of the index, their
 * links followed; the thanks page trusts the return alone, as the legacy one did: the
 * webhook, not this page, records the donation. Its lit seal answers the cancel page's
 * dormant one.
 */
@Component({
  selector: 'lodb-donation-outcome-page',
  imports: [Button, Frame, RouterLink, TranslocoPipe],
  providers: [provideTranslocoScope('donate')],
  templateUrl: './donation-outcome-page.html',
  styleUrl: './donation-outcome-page.css',
  host: { class: 'flex flex-1 flex-col' },
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class DonationOutcomePage {
  protected readonly outcome = injectRouteData<DonationOutcome>('outcome');
  private readonly locale = inject(PageDirection).locale;

  constructor() {
    applyDonateHead((translate) => ({
      title: translate(`donate.${this.outcome()}.title`),
      titleFormat: 'account',
      kind: 'donation-return',
      path: 'donate',
    }));
  }

  protected link(path: string): string {
    return localePath(this.locale(), path);
  }
}
