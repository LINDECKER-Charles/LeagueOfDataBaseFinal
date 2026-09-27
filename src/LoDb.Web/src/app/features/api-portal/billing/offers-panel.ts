import { ChangeDetectionStrategy, Component, computed, input, output } from '@angular/core';
import { TranslocoPipe } from '@jsverse/transloco';
import type { ApiKeyOverview } from '../../../core/api/generated/models/api-key-overview';
import type { BillingOffers } from '../../../core/api/generated/models/billing-offers';
import { Button } from '../../../ui/controls/button';
import { Chip } from '../../../ui/controls/chip';
import { Frame } from '../../../ui/surfaces/frame';
import { formatCount } from '../key/usage-format';
import type { PortalPurchase } from '../state/api-key-portal';

const CENTS_PER_EURO = 100;
const YEAR = 'year';

/** A pack as the panel shows it: its price in euros, its requests formatted. */
interface PackCard {
  readonly code: string;
  readonly price: number;
  readonly volume: string;
}

/** A plan as the panel shows it, with its rate and its billing period. */
interface PlanCard extends PackCard {
  readonly rate: number;
  readonly yearly: boolean;
}

/**
 * The packs and plans on sale for the key: Stripe's page opens on a choice, and the
 * entitlements land by webhook. A key already subscribed buys no second plan. Without
 * Stripe behind the API, nothing is on sale and the panel says so.
 */
@Component({
  selector: 'lodb-offers-panel',
  imports: [Button, Chip, Frame, TranslocoPipe],
  templateUrl: './offers-panel.html',
  styleUrls: ['../shared/portal.css', './offers-panel.css'],
  host: { class: 'block' },
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class OffersPanel {
  /** Null when the API could not say what is on sale. */
  readonly offers = input.required<BillingOffers | null>();
  readonly key = input.required<ApiKeyOverview>();
  readonly busy = input(false);

  /** A choice, without its locale: the page adds it. */
  readonly buy = output<Omit<PortalPurchase, 'locale'>>();

  protected readonly available = computed(() => this.offers()?.available === true);
  protected readonly packs = computed<readonly PackCard[]>(() =>
    (this.offers()?.packs ?? []).map((pack) => ({
      code: pack.code,
      price: pack.priceCents / CENTS_PER_EURO,
      volume: formatCount(pack.requests),
    })),
  );
  protected readonly plans = computed<readonly PlanCard[]>(() =>
    (this.offers()?.plans ?? []).map((plan) => ({
      code: plan.code,
      price: plan.priceCents / CENTS_PER_EURO,
      volume: formatCount(plan.monthlyQuota),
      rate: plan.ratePerMinute,
      yearly: plan.interval === YEAR,
    })),
  );
}
