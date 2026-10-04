import {
  ChangeDetectionStrategy,
  Component,
  DestroyRef,
  computed,
  inject,
  linkedSignal,
} from '@angular/core';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { TranslocoPipe } from '@jsverse/transloco';
import type { VoteState } from '../../../core/api/generated/models/vote-state';
import { TrendsService } from '../../../core/api/generated/services/trends.service';
import { injectRouteData } from '../../../core/routing/inject-route-data';
import { Chip } from '../../../ui/controls/chip';
import { Backdrop } from '../../../ui/surfaces/backdrop';
import { whenSignedIn } from '../shared/session/when-signed-in';
import { ForgeCta } from './cta/forge-cta';
import { TrendFilters } from './filters/trend-filters';
import { applyTrendsHead } from './head/apply-trends-head';
import type { TrendsView } from './loading/trends-view';
import { TrendsPager } from './pager/trends-pager';
import { TrendRow } from './rows/trend-row';
import { votedRows } from './rows/voted-rows';

/**
 * The trends, `/{locale}/trends`: every public build ranked by its score, filtered by
 * champion, mode and language and paged through the query, rendered on the server without an
 * account (`resolveTrends`). A signed-in reader's own votes are read again in the browser.
 */
@Component({
  selector: 'lodb-trends-page',
  imports: [Backdrop, Chip, ForgeCta, TranslocoPipe, TrendFilters, TrendRow, TrendsPager],
  templateUrl: './trends-page.html',
  styleUrl: './trends-page.css',
  host: { class: 'flex flex-1 flex-col' },
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class TrendsPage {
  protected readonly view = injectRouteData<TrendsView>('trends');

  private readonly trends = inject(TrendsService);
  private readonly destroyRef = inject(DestroyRef);
  // The reader's own scores, forgotten with the page they were read for.
  private readonly votes = linkedSignal<TrendsView, ReadonlyMap<number, VoteState>>({
    source: this.view,
    computation: () => new Map(),
  });

  protected readonly rows = computed(() => votedRows(this.view().page.rows, this.votes()));
  protected readonly action = computed(() => `/${this.view().locale}/trends`);

  constructor() {
    applyTrendsHead(this.view);
    whenSignedIn(this.view, (view) => this.readVotes(view));
  }

  // A failed read keeps the anonymous scores; the arrows still vote.
  private readVotes(view: TrendsView): void {
    this.trends
      .listTrends(view.request)
      .pipe(takeUntilDestroyed(this.destroyRef))
      .subscribe({
        next: (page) => {
          if (this.view() !== view) return;
          const entries = page.rows.map(
            (row) => [row.id, { score: row.score, myVote: row.myVote ?? 0 }] as const,
          );
          this.votes.set(new Map(entries));
        },
        error: () => undefined,
      });
  }
}
