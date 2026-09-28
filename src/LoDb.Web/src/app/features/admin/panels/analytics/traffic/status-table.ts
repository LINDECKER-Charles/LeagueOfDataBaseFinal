import { ChangeDetectionStrategy, Component, input } from '@angular/core';
import type { AnalyticsRank } from '../../../../../core/api/generated/models/analytics-rank';
import { Chip } from '../../../../../ui/controls/chip';
import { FigurePipe } from '../../../format/figure-pipe';
import { AdminTextPipe } from '../../../shared/admin-text-pipe';
import { foldRows } from '../../../widgets/fold-rows';
import { FoldToggle } from '../../../widgets/fold-toggle';

// The legacy table showed six statuses before its toggle.
const LIMIT = 6;

/**
 * The HTTP statuses served over the period, the legacy "Codes de réponse" table: each status
 * as a chip, its views and its share of them, six before the rest folds away.
 */
@Component({
  selector: 'lodb-status-table',
  imports: [Chip, FigurePipe, FoldToggle, AdminTextPipe],
  template: `
    <div class="hx-table-scroll">
      <table class="hx-table hx-table--flush admin-tbl">
        <thead>
          <tr>
            <th scope="col">{{ 'admin.traffic.columns.status' | adminText }}</th>
            <th scope="col" class="num">{{ 'admin.traffic.columns.views' | adminText }}</th>
            <th scope="col" class="num">{{ 'admin.traffic.columns.share' | adminText }}</th>
          </tr>
        </thead>
        <tbody>
          @for (row of fold.shown(); track row.name) {
            <tr>
              <td>
                <span lodbChip>{{ row.name }}</span>
              </td>
              <td class="num">{{ row.count | figure }}</td>
              <td class="num">{{ row.pct | figure: 'pct' }}</td>
            </tr>
          } @empty {
            <tr>
              <td colspan="3" class="t-empty">{{ 'admin.common.no_data' | adminText }}</td>
            </tr>
          }
        </tbody>
      </table>
    </div>
    <lodb-fold-toggle [folded]="fold.folded()" [(open)]="fold.open" />
  `,
  host: { class: 'block' },
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class StatusTable {
  readonly statuses = input.required<readonly AnalyticsRank[]>();

  protected readonly fold = foldRows(
    () => this.statuses(),
    () => LIMIT,
  );
}
