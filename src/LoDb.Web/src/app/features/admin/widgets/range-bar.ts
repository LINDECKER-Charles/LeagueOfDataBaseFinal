import { ChangeDetectionStrategy, Component, input } from '@angular/core';
import { RouterLink } from '@angular/router';
import { Button } from '../../../ui/controls/button';
import { ADMIN_RANGES } from '../shared/admin-range';
import { AdminTextPipe } from '../shared/admin-text-pipe';

/** The period of an analytics panel, kept in the URL (`?range=`). */
@Component({
  selector: 'lodb-range-bar',
  imports: [Button, RouterLink, AdminTextPipe],
  template: `
    <nav class="flex flex-wrap gap-1.5" [attr.aria-label]="'admin.range.label' | adminText">
      @for (range of ranges; track range) {
        <a
          [lodbButton]="range === current() ? 'primary' : 'ghost'"
          [routerLink]="[]"
          [queryParams]="{ range: range, page: null }"
          queryParamsHandling="merge"
          [attr.aria-current]="range === current() ? 'page' : null"
          >{{ 'admin.range.' + range | adminText }}</a
        >
      }
    </nav>
  `,
  host: { class: 'block' },
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class RangeBar {
  readonly current = input.required<string>();

  protected readonly ranges = ADMIN_RANGES;
}
