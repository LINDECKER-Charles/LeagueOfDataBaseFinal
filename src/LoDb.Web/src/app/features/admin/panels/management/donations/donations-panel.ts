import { ChangeDetectionStrategy, Component, computed } from '@angular/core';
import { RouterLink } from '@angular/router';
import { TranslocoPipe } from '@jsverse/transloco';
import { listAdminDonations } from '../../../../../core/api/generated/fn/admin-donations/list-admin-donations';
import type { AdminDonationRow } from '../../../../../core/api/generated/models/admin-donation-row';
import { TimeSeriesChart } from '../../../charts/time-series-chart';
import { FigurePipe } from '../../../format/figure-pipe';
import { StampPipe } from '../../../format/stamp-pipe';
import { injectAdminText } from '../../../shared/inject-admin-text';
import { injectQuery } from '../../../shared/inject-query';
import { injectPanel } from '../../../state/inject-panel';
import { PanelState } from '../../../state/panel-state';
import { AdminCard } from '../../../widgets/admin-card';
import { AdminPager } from '../../../widgets/admin-pager';
import { Badge } from '../../../widgets/badge';
import { Kpi } from '../../../widgets/kpi';
import { PageHead } from '../../../widgets/page-head';

// The currency the amounts are written in; another one is named next to its amount.
const EURO = 'eur';

/**
 * `/admin/donations`: what was given, day by day, and every donation with its donor when
 * signed in. Read only: a refund goes through Stripe.
 */
@Component({
  selector: 'lodb-donations-panel',
  imports: [
    AdminCard,
    AdminPager,
    Badge,
    FigurePipe,
    Kpi,
    PageHead,
    PanelState,
    RouterLink,
    StampPipe,
    TimeSeriesChart,
    TranslocoPipe,
  ],
  templateUrl: './donations-panel.html',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class DonationsPanel {
  private readonly texts = injectAdminText();

  protected readonly query = injectQuery();
  protected readonly donations = injectPanel(listAdminDonations, () => ({
    page: this.query.page(),
  }));
  protected readonly daily = computed(() => {
    const days = this.donations.value()?.daily ?? [];
    return {
      dates: days.map((day) => day.date),
      series: [
        {
          label: this.texts.text('donations.series.amount'),
          color: 'var(--color-gold)',
          values: days.map((day) => day.cents),
          format: 'euros' as const,
        },
      ],
    };
  });

  protected isForeign(donation: AdminDonationRow): boolean {
    return donation.currency.toLowerCase() !== EURO;
  }
}
