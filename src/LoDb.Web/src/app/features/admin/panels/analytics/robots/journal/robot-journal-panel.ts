import { ChangeDetectionStrategy, Component } from '@angular/core';
import { getRobotHits } from '../../../../../../core/api/generated/fn/admin-analytics/get-robot-hits';
import { AdminCard } from '../../../../layout/admin-card';
import { PageHead } from '../../../../layout/page-head';
import { AdminTextPipe } from '../../../../shared/admin-text-pipe';
import { injectQuery } from '../../../../shared/inject-query';
import { injectPanel } from '../../../../state/inject-panel';
import { PanelState } from '../../../../state/panel-state';
import { AdminPager } from '../../../../widgets/admin-pager';
import { RobotHitsTable } from '../shared/robot-hits-table';
import { RobotTabs } from '../shared/robot-tabs';
import { RobotJournalFilters } from './robot-journal-filters';

/**
 * `/admin/robots/journal`: the hits of robots kept (the last 30 days), newest first, 50 a
 * page, filtered by robot, category, class of statuses, verdict, section and a text of the
 * path, every filter in the URL.
 */
@Component({
  selector: 'lodb-robot-journal-panel',
  imports: [
    AdminCard,
    AdminPager,
    PageHead,
    PanelState,
    RobotHitsTable,
    RobotJournalFilters,
    RobotTabs,
    AdminTextPipe,
  ],
  template: `
    <lodb-page-head
      [eyebrow]="'admin.eyebrows.analytics' | adminText"
      [title]="'admin.robots.journal.title' | adminText"
      [subtitle]="'admin.robots.journal.lede' | adminText"
    >
      <lodb-robot-tabs current="journal" />
    </lodb-page-head>
    <lodb-robot-journal-filters />
    <lodb-panel-state [panel]="hits" />
    @if (hits.value(); as page) {
      <lodb-admin-card [attr.aria-busy]="hits.busy()">
        <lodb-robot-hits-table [hits]="page.items" />
        <lodb-admin-pager [page]="page.page" [hasMore]="page.hasMore" />
      </lodb-admin-card>
    }
  `,
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class RobotJournalPanel {
  private readonly query = injectQuery();

  protected readonly hits = injectPanel(getRobotHits, () => {
    const text = (name: string) => this.query.text(name) || undefined;
    const status = Number(this.query.text('status'));
    return {
      robot: text('robot'),
      category: text('category'),
      status: Number.isInteger(status) && status > 0 ? status : undefined,
      verdict: text('verdict'),
      section: text('section'),
      q: text('q'),
      page: this.query.page(),
    };
  });
}
