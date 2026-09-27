import { ChangeDetectionStrategy, Component, computed, signal } from '@angular/core';
import { RouterLink } from '@angular/router';
import { TranslocoPipe } from '@jsverse/transloco';
import { creditAdminApiClient } from '../../../../../core/api/generated/fn/admin-api-clients/credit-admin-api-client';
import { listAdminApiClients } from '../../../../../core/api/generated/fn/admin-api-clients/list-admin-api-clients';
import { revokeAdminApiClient } from '../../../../../core/api/generated/fn/admin-api-clients/revoke-admin-api-client';
import type { AdminApiClientRow } from '../../../../../core/api/generated/models/admin-api-client-row';
import { Button } from '../../../../../ui/controls/button';
import { Field } from '../../../../../ui/controls/field';
import { DonutChart } from '../../../charts/donut-chart';
import { paletteSlices } from '../../../charts/palette-slices';
import { figure } from '../../../format/figure';
import { FigurePipe } from '../../../format/figure-pipe';
import { StampPipe } from '../../../format/stamp-pipe';
import { formText } from '../../../shared/form-text';
import { injectRowActions } from '../../../shared/http/inject-row-actions';
import { injectAdminText } from '../../../shared/inject-admin-text';
import { injectQuery } from '../../../shared/inject-query';
import { injectPanel } from '../../../state/inject-panel';
import { PanelState } from '../../../state/panel-state';
import { AdminCard } from '../../../widgets/admin-card';
import { AdminPager } from '../../../widgets/admin-pager';
import { Badge } from '../../../widgets/badge';
import { ConfirmButton } from '../../../widgets/confirm-button';
import { Kpi } from '../../../widgets/kpi';
import { PageHead } from '../../../widgets/page-head';
import { RankList } from '../../../widgets/rank-list';

/** The requests a credit adds, as the API bounds them. */
const CREDIT = { min: 1, max: 1_000_000 } as const;

/**
 * `/admin/api-clients`: the keys of the public API. Their plans as a ring, the most used of
 * the month ranked, and every key with its quota. An administrator credits a key with
 * prepaid requests or revokes it.
 */
@Component({
  selector: 'lodb-api-clients-panel',
  imports: [
    AdminCard,
    AdminPager,
    Badge,
    Button,
    ConfirmButton,
    DonutChart,
    Field,
    FigurePipe,
    Kpi,
    PageHead,
    PanelState,
    RankList,
    RouterLink,
    StampPipe,
    TranslocoPipe,
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
  /** The key whose credit form is open. */
  protected readonly crediting = signal<number | null>(null);
  protected readonly plans = computed(() =>
    paletteSlices(
      (this.clients.value()?.kpis.byPlan ?? []).map((plan) => ({
        name: this.texts.term('api_clients.plans', plan.plan),
        value: plan.keys,
      })),
      this.texts.text('common.others'),
    ),
  );
  protected readonly consumers = computed(() =>
    (this.clients.value()?.topConsumers ?? []).map((key) => ({
      name: `${key.username} · ${key.keyPrefix}`,
      value: key.requests,
    })),
  );

  protected planOf(client: AdminApiClientRow): string {
    return this.texts.term('api_clients.plans', client.plan);
  }

  protected async grant(event: Event, client: AdminApiClientRow): Promise<void> {
    const requests = Number(formText(event, 'requests'));
    const receipt = await this.actions.run(client.id, {
      call: creditAdminApiClient,
      params: { id: client.id, body: { requests } },
      done: {
        key: 'api_clients.done.credit',
        params: { name: client.name, count: figure(requests) },
      },
    });
    if (receipt !== null) {
      this.crediting.set(null);
    }
  }

  protected revoke(client: AdminApiClientRow): void {
    void this.actions.run(client.id, {
      call: revokeAdminApiClient,
      params: { id: client.id },
      done: { key: 'api_clients.done.revoke', params: { name: client.name } },
    });
  }
}
