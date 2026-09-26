import { ChangeDetectionStrategy, Component, computed, inject } from '@angular/core';
import { TranslocoPipe } from '@jsverse/transloco';
import { PAGE_SIZE_ALL } from '../filtering/grid/page-size-all';
import { CatalogueFilter } from '../state/catalogue-filter';
import { PAGE_SIZES } from '../state/page-sizes';
import { isPlainClick } from './is-plain-click';

/** A step of the pager: where it leads, or null at either end of the list. */
interface PagerStep {
  readonly rel: 'prev' | 'next';
  readonly page: number | null;
  readonly href: string;
  readonly label: string;
  /** Bidi-mirrored angle quotes, so the arrows turn round in right-to-left pages. */
  readonly glyph: string;
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
        glyph: '‹',
      },
      { rel: 'next', page: next, href: this.hrefOf(next), label: 'filter.next', glyph: '›' },
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
