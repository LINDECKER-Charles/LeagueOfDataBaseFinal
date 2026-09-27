import { ChangeDetectionStrategy, Component, computed } from '@angular/core';
import { creditAdminApiClient } from '../../../../../core/api/generated/fn/admin-api-clients/credit-admin-api-client';
import { listAdminApiClients } from '../../../../../core/api/generated/fn/admin-api-clients/list-admin-api-clients';
import { revokeAdminApiClient } from '../../../../../core/api/generated/fn/admin-api-clients/revoke-admin-api-client';
import type { AdminApiClientRow } from '../../../../../core/api/generated/models/admin-api-client-row';
import { Button } from '../../../../../ui/controls/button';
import { Chip } from '../../../../../ui/controls/chip';
import { categorySlices } from '../../../charts/category-slices';
import { DonutChart } from '../../../charts/donut-chart';
import { figure } from '../../../format/figure';
import { FigurePipe } from '../../../format/figure-pipe';
import { StampPipe } from '../../../format/stamp-pipe';
import { AdminCard } from '../../../layout/admin-card';
import { AdminRule } from '../../../layout/admin-rule';
import { PageHead } from '../../../layout/page-head';
import { AdminTextPipe } from '../../../shared/admin-text-pipe';
import { formText } from '../../../shared/form-text';
import { injectRowActions } from '../../../shared/http/inject-row-actions';
import { injectAdminText } from '../../../shared/inject-admin-text';
import { injectQuery } from '../../../shared/inject-query';
import { injectPanel } from '../../../state/inject-panel';
import { PanelState } from '../../../state/panel-state';
import { AdminPager } from '../../../widgets/admin-pager';
import { Badge } from '../../../widgets/badge';
import { ConfirmButton } from '../../../widgets/confirm-button';
import { Kpi } from '../../../widgets/kpi';
import { RankList } from '../../../widgets/rank-list';

/** The requests a credit adds, as the API bounds them. */
const CREDIT = { min: 1, max: 1_000_000 } as const;

/**
 * `/admin/api-clients`: the keys of the public API, in the order of the legacy page. Their
 * plans as a ring, the most used of the last 30 days, then every key with its quota. An
 * administrator credits an active key with prepaid requests, typed in its row, or revokes it.
 */
@Component({
  selector: 'lodb-api-clients-panel',
  imports: [
    AdminCard,
    AdminPager,
    AdminRule,
    Badge,
    Button,
    Chip,
    ConfirmButton,
    DonutChart,
    FigurePipe,
    Kpi,
    PageHead,
    PanelState,
    RankList,
    StampPipe,
    AdminTextPipe,
  ],
  templateUrl: './api-clients-panel.html',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class ApiClientsPanel {
  private readonly texts = injectAdminText();

  protected readonly credit = CREDIT;
  protected readonly query = injectQuery();
  protected readonly clients = injectPanel(listAdminApiClients, () => ({
    page: this.query.page(),
  }));
  protected readonly actions = injectRowActions(() => this.clients.reload());
  protected readonly plans = computed(() =>
    categorySlices(
      'plan',
      (this.clients.value()?.kpis.byPlan ?? []).map((plan) => ({
        name: plan.plan,
        value: plan.keys,
      })),
      (plan) => this.texts.term('api_clients.plans', plan),
    ),
  );
  protected readonly consumers = computed(() =>
    (this.clients.value()?.topConsumers ?? []).map((key) => ({
      name: `${key.keyPrefix}… — ${key.username}`,
      value: key.requests,
    })),
  );

  protected planOf(client: AdminApiClientRow): string {
    return this.texts.term('api_clients.plans', client.plan);
  }

  protected async grant(event: Event, client: AdminApiClientRow): Promise<void> {
    const form = event.target as HTMLFormElement;
    const requests = Number(formText(event, 'requests'));
    const receipt = await this.actions.run(client.id, {
      call: creditAdminApiClient,
      params: { id: client.id, body: { requests } },
      done: {
        key: 'api_clients.done.credit',
        params: { prefix: client.keyPrefix, count: figure(requests) },
      },
    });
    if (receipt !== null) {
      form.reset();
    }
  }

  protected revoke(client: AdminApiClientRow): void {
    void this.actions.run(client.id, {
      call: revokeAdminApiClient,
      params: { id: client.id },
      done: { key: 'api_clients.done.revoke', params: { prefix: client.keyPrefix } },
    });
  }
}
