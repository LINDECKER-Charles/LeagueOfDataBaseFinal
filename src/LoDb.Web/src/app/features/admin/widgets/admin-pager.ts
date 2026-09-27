import { ChangeDetectionStrategy, Component, computed, input } from '@angular/core';
import { RouterLink } from '@angular/router';
import { Button } from '../../../ui/controls/button';
import { AdminTextPipe } from '../shared/admin-text-pipe';

/**
 * Previous and next pages of a list, kept in the URL (`?page=`) with the other filters, so
 * that the history and a shared link come back to the same page. `pages` null is a journal
 * read by cursor: the total is unknown and the next page shows while `hasMore` says so.
 */
@Component({
  selector: 'lodb-admin-pager',
  imports: [Button, RouterLink, AdminTextPipe],
  template: `
    @if (page() > 1 || hasNext()) {
      <nav
        class="mt-5 flex items-center justify-center gap-3"
        [attr.aria-label]="'admin.pager.label' | adminText"
      >
        @if (page() > 1) {
          <a
            lodbButton="ghost"
            [routerLink]="[]"
            [queryParams]="{ page: page() - 1 }"
            queryParamsHandling="merge"
            >← {{ 'admin.pager.previous' | adminText }}</a
          >
        }
        <span class="font-mono text-sm text-text-muted">
          @if (pages() === null) {
            {{ 'admin.pager.page' | adminText: { page: page() } }}
          } @else {
            {{ page() }} / {{ pages() }}
          }
        </span>
        @if (hasNext()) {
          <a
            lodbButton="ghost"
            [routerLink]="[]"
            [queryParams]="{ page: page() + 1 }"
            queryParamsHandling="merge"
            >{{ 'admin.pager.next' | adminText }} →</a
          >
        }
      </nav>
    }
  `,
  host: { class: 'block' },
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class AdminPager {
  readonly page = input.required<number>();
  /** The number of pages; null when the list does not know it. */
  readonly pages = input<number | null>(null);
  readonly hasMore = input(false);

  protected readonly hasNext = computed(() => {
    const pages = this.pages();
    return pages === null ? this.hasMore() : this.page() < pages;
  });
}
