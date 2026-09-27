import { ChangeDetectionStrategy, Component, computed, input } from '@angular/core';
import { RouterLink } from '@angular/router';
import { Button } from '../../../ui/controls/button';
import { AdminTextPipe } from '../shared/admin-text-pipe';

/**
 * Previous and next pages of a list, kept in the URL (`?page=`) with the other filters, so
 * that the history and a shared link come back to the same page. `pages` null is a journal
 * read by cursor: the total is unknown and the next page shows while `hasMore` says so. As in
 * the legacy admin, both directions always show once there is more than one page, the one
 * that leads nowhere greyed out.
 */
@Component({
  selector: 'lodb-admin-pager',
  imports: [Button, RouterLink, AdminTextPipe],
  template: `
    @if (page() > 1 || hasNext()) {
      <nav
        class="mt-[1.3rem] flex items-center justify-center gap-[0.9rem]"
        [attr.aria-label]="'admin.pager.label' | adminText"
      >
        @if (page() > 1) {
          <a
            lodbButton="ghost"
            lodbButtonSize="small"
            [routerLink]="[]"
            [queryParams]="{ page: page() - 1 }"
            queryParamsHandling="merge"
            >← {{ 'admin.pager.previous' | adminText }}</a
          >
        } @else {
          <span [class]="disabled" aria-disabled="true">
            ← {{ 'admin.pager.previous' | adminText }}
          </span>
        }
        <span class="font-mono text-[0.8rem] text-text-muted tabular-nums">
          @if (pages() === null) {
            {{ 'admin.pager.page' | adminText: { page: page() } }}
          } @else {
            {{ page() }} / {{ pages() }}
          }
        </span>
        @if (hasNext()) {
          <a
            lodbButton="ghost"
            lodbButtonSize="small"
            [routerLink]="[]"
            [queryParams]="{ page: page() + 1 }"
            queryParamsHandling="merge"
            >{{ 'admin.pager.next' | adminText }} →</a
          >
        } @else {
          <span [class]="disabled" aria-disabled="true">
            {{ 'admin.pager.next' | adminText }} →
          </span>
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

  // The small ghost button, greyed out: spelled out whole for the Tailwind scanner.
  protected readonly disabled = 'hx-btn-ghost hx-btn-sm pointer-events-none opacity-35';
  protected readonly hasNext = computed(() => {
    const pages = this.pages();
    return pages === null ? this.hasMore() : this.page() < pages;
  });
}
