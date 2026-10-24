import { ChangeDetectionStrategy, Component, computed, input } from '@angular/core';
import { getAnalyticsBreakdown } from '../../../../../../core/api/generated/fn/admin-analytics/get-analytics-breakdown';
import type { Segment } from '../../../../widgets/segment-bar';
import { FigurePipe } from '../../../../format/figure-pipe';
import { AdminTextPipe } from '../../../../shared/admin-text-pipe';
import { formText } from '../../../../shared/form-text';
import { injectAdminText } from '../../../../shared/inject-admin-text';
import { injectQuery } from '../../../../shared/inject-query';
import { injectPanel } from '../../../../state/inject-panel';
import { PanelState } from '../../../../state/panel-state';
import { AdminPager } from '../../../../widgets/admin-pager';
import { SegmentBar } from '../../../../widgets/segment-bar';
import { type ExplorerDimension, explorerRows } from './explorer-rows';

const DIMENSIONS: readonly ExplorerDimension[] = ['pages', 'entities', 'referers'];
const SEGMENTS: readonly Segment[] = DIMENSIONS.map((dimension) => ({
  // The pages are the default: no parameter at all.
  value: dimension === 'pages' ? '' : dimension,
  label: `admin.traffic.explorer.dimensions.${dimension}`,
}));
const DIMENSION = 'dim';
const PERCENT = 100;
const SEARCH = 'q';

/**
 * Every page, entity or referring host of the period, past the tops of the report: searched
 * by any text of the key, the most viewed first, 50 a page, each with its share of the whole
 * dimension. The dimension, the search and the page live in the URL.
 */
@Component({
  selector: 'lodb-traffic-explorer',
  imports: [AdminPager, FigurePipe, PanelState, SegmentBar, AdminTextPipe],
  templateUrl: './traffic-explorer.html',
  host: { class: 'block' },
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class TrafficExplorer {
  /** The period of the page. */
  readonly range = input.required<string>();

  private readonly texts = injectAdminText();

  protected readonly query = injectQuery();
  protected readonly segments = SEGMENTS;
  protected readonly dimension = computed<ExplorerDimension>(() => {
    const value = this.query.text(DIMENSION);
    return DIMENSIONS.includes(value as ExplorerDimension) ? (value as ExplorerDimension) : 'pages';
  });
  protected readonly breakdown = injectPanel(getAnalyticsBreakdown, () => ({
    range: this.range(),
    dimension: this.dimension(),
    q: this.query.text(SEARCH) || undefined,
    page: this.query.page(),
  }));
  /** The keys of the page, their bars sized on the most viewed of them. */
  protected readonly rows = computed(() => {
    const rows = explorerRows(this.dimension(), this.breakdown.value()?.rows ?? [], this.texts);
    const max = Math.max(1, ...rows.map((row) => row.count));
    return rows.map((row) => ({ ...row, bar: (row.count / max) * PERCENT }));
  });

  protected search(event: Event): void {
    this.query.set({ [SEARCH]: formText(event, SEARCH).trim() || null });
  }
}
