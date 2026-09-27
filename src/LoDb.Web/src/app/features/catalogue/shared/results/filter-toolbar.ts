import { ChangeDetectionStrategy, Component, computed, inject } from '@angular/core';
import { TranslocoPipe } from '@jsverse/transloco';
import { PAGE_SIZE_ALL } from '../filtering/grid/page-size-all';
import { CatalogueFilter } from '../state/catalogue-filter';
import { PAGE_SIZES } from '../state/page-sizes';
import { isPlainClick } from './is-plain-click';
import { splitAroundCount } from './split-around-count';

// The legacy pager's chevrons, drawn toward the inline start and end; mirrored in RTL.
const PREVIOUS_CHEVRON = 'M15 6l-6 6 6 6';
const NEXT_CHEVRON = 'M9 6l6 6-6 6';

/** A step of the pager: where it leads, or null at either end of the list. */
interface PagerStep {
  readonly rel: 'prev' | 'next';
  readonly page: number | null;
  readonly href: string;
  readonly label: string;
  /** Path of its chevron, in a 24-unit box. */
  readonly chevron: string;
}

/**
 * The head of the results: how many cards match and where the reader stands, the page
 * sizes, the pager. The pager steps are real links to the next page of the list, so the
 * server-rendered pages chain for crawlers; a plain click turns the page in place.
 */
@Component({
  selector: 'lodb-filter-toolbar',
  imports: [TranslocoPipe],
  templateUrl: './filter-toolbar.html',
  styleUrl: './filter-toolbar.css',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class FilterToolbar {
  protected readonly filter = inject(CatalogueFilter);
  protected readonly sizes = PAGE_SIZES;
  protected readonly all = PAGE_SIZE_ALL;
  /** The count cut around its figure: the figure in cyan mono, the words in the body face. */
  protected readonly split = splitAroundCount;

  protected readonly steps = computed<PagerStep[]>(() => {
    const { page, pageCount } = this.filter.view();
    const previous = page > 1 ? page - 1 : null;
    const next = page < pageCount ? page + 1 : null;
    return [
      {
        rel: 'prev',
        page: previous,
        href: this.hrefOf(previous),
        label: 'filter.prev',
        chevron: PREVIOUS_CHEVRON,
      },
      {
        rel: 'next',
        page: next,
        href: this.hrefOf(next),
        label: 'filter.next',
        chevron: NEXT_CHEVRON,
      },
    ];
  });

  protected go(event: MouseEvent, page: number): void {
    if (isPlainClick(event)) {
      event.preventDefault();
      this.filter.goTo(page);
    }
  }

  private hrefOf(page: number | null): string {
    return page === null ? '' : this.filter.urlOf({ page });
  }
}
