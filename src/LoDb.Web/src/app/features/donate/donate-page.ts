import { HttpClient } from '@angular/common/http';
import {
  ChangeDetectionStrategy,
  Component,
  computed,
  inject,
  linkedSignal,
  signal,
} from '@angular/core';
import { RouterLink } from '@angular/router';
import { provideTranslocoScope, TranslocoPipe } from '@jsverse/transloco';
import { firstValueFrom } from 'rxjs';
import { API_BASE_URL } from '../../core/api/api-base-url';
import { openDonationCheckout } from '../../core/api/generated/fn/donations/open-donation-checkout';
import type { DonationOptions } from '../../core/api/generated/models/donation-options';
import { PageDirection } from '../../core/layout/direction/page-direction';
import { localePath } from '../../core/layout/shell/locale-path';
import { injectRouteData } from '../../core/routing/inject-route-data';
import { breadcrumbList } from '../../core/seo/json-ld/site/breadcrumb-list';
import { Button } from '../../ui/controls/button';
import { Field } from '../../ui/controls/field';
import { Frame } from '../../ui/surfaces/frame';
import { applyDonateHead } from './apply-donate-head';
import { checkoutFailure } from './checkout/checkout-failure';
import { parseEuroAmount } from './checkout/parse-euro-amount';
import { StripeRedirect } from './checkout/stripe-redirect';

/** A tier of the form: its amount, and the name of the legacy offerings when it is one. */
interface DonationTier {
  readonly cents: number;
  readonly euros: number;
  readonly name: string | null;
}

const TIER_NAMES: Readonly<Partial<Record<number, string>>> = {
  300: 'donate.tier.spark',
  500: 'donate.tier.gem',
  1_000: 'donate.tier.crest',
  2_500: 'donate.tier.relic',
};

function tierOf(cents: number): DonationTier {
  return { cents, euros: cents / 100, name: TIER_NAMES[cents] ?? null };
}

/**
 * `/{locale}/donate`: a tier or a free amount, then Stripe's hosted page, which the API
 * opens for the amount and the page follows. The donor comes back to `donate/success` or
 * `donate/cancel`. Without Stripe behind the API, the form stays closed and says so. Only a
 * build with payments routes here (ADR 0007).
 */
@Component({
  selector: 'lodb-donate-page',
  imports: [Button, Field, Frame, RouterLink, TranslocoPipe],
  providers: [provideTranslocoScope('donate')],
  templateUrl: './donate-page.html',
  styleUrl: './donate-page.css',
  host: { class: 'block' },
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class DonatePage {
  protected readonly options = injectRouteData<DonationOptions>('options');
  protected readonly locale = inject(PageDirection).locale;
  private readonly http = inject(HttpClient);
  private readonly apiOrigin = inject(API_BASE_URL);
  private readonly redirect = inject(StripeRedirect);

  protected readonly tiers = computed(() => this.options().presets.map(tierOf));
  protected readonly bounds = computed(() => ({
    min: this.options().minCents / 100,
    max: this.options().maxCents / 100,
  }));
  // The second tier, as the legacy form: the first one reads as the least one may give.
  protected readonly chosen = linkedSignal(() => {
    const presets = this.options().presets;
    return presets[1] ?? presets[0];
  });
  protected readonly amount = signal('');
  protected readonly busy = signal(false);
  protected readonly error = signal<string | null>(null);

  constructor() {
    applyDonateHead((translate) => ({
      title: translate('seo.donate.title'),
      description: translate('seo.donate.description'),
      path: 'donate',
      jsonLd: (urls) => [
        breadcrumbList([
          { name: translate('header.navigation.home'), url: urls.page('') },
          { name: translate('donate.title'), url: urls.canonical },
        ]),
      ],
    }));
  }

  protected link(path: string): string {
    return localePath(this.locale(), path);
  }

  protected type(event: Event): void {
    this.amount.set((event.target as HTMLInputElement).value);
  }

  protected submit(event: Event): void {
    event.preventDefault();
    if (this.busy() || !this.options().available) {
      return;
    }
    const amountCents = this.amountCents();
    if (amountCents === null) {
      this.error.set('donate.error.invalid_amount');
      return;
    }
    void this.open(amountCents);
  }

  // A free amount, once typed, wins over the tier.
  private amountCents(): number | null {
    const typed = this.amount().trim();
    const cents = typed === '' ? this.chosen() : parseEuroAmount(typed);
    const { minCents, maxCents } = this.options();
    return cents !== null && cents >= minCents && cents <= maxCents ? cents : null;
  }

  private async open(amountCents: number): Promise<void> {
    this.busy.set(true);
    this.error.set(null);
    try {
      const body = { amountCents, locale: this.locale() };
      const created = await firstValueFrom(
        openDonationCheckout(this.http, this.apiOrigin, { body }),
      );
      if (!this.redirect.go(created.body.url)) {
        this.error.set('donate.error.gateway');
      }
    } catch (error) {
      this.error.set(checkoutFailure(error));
    } finally {
      this.busy.set(false);
    }
  }
}
