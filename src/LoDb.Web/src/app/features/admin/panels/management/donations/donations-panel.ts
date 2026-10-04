import { ChangeDetectionStrategy, Component, computed } from '@angular/core';
import { listAdminDonations } from '../../../../../core/api/generated/fn/admin-donations/list-admin-donations';
import { Sparkline } from '../../../charts/sparkline';
import { FigurePipe } from '../../../format/figure-pipe';
import { StampPipe } from '../../../format/stamp-pipe';
import { AdminCard } from '../../../layout/admin-card';
import { AdminRule } from '../../../layout/admin-rule';
import { PageHead } from '../../../layout/page-head';
import { AdminTextPipe } from '../../../shared/admin-text-pipe';
import { injectQuery } from '../../../shared/inject-query';
import { injectPanel } from '../../../state/inject-panel';
import { PanelState } from '../../../state/panel-state';
import { AdminPager } from '../../../widgets/admin-pager';
import { Badge } from '../../../widgets/badge';
import { Kpi } from '../../../widgets/kpi';

/**
 * `/admin/donations`: what was given, the last 30 days as a trend, and every donation with
 * its account when one was signed in, as the legacy ledger showed them. Read only: a refund
 * goes through Stripe.
 */
@Component({
  selector: 'lodb-donations-panel',
  imports: [
    AdminCard,
    AdminPager,
    AdminRule,
    Badge,
    FigurePipe,
    Kpi,
    PageHead,
    PanelState,
    Sparkline,
    StampPipe,
    AdminTextPipe,
  ],
  templateUrl: './donations-panel.html',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class DonationsPanel {
  protected readonly query = injectQuery();
  protected readonly donations = injectPanel(listAdminDonations, () => ({
    page: this.query.page(),
  }));
  /** The cents given each of the last 30 days, oldest first. */
  protected readonly daily = computed(() =>
    (this.donations.value()?.daily ?? []).map((day) => day.cents),
  );
  protected readonly monthCents = computed(() =>
    this.daily().reduce((sum, cents) => sum + cents, 0),
  );
}
