import { ChangeDetectionStrategy, Component, inject } from '@angular/core';
import { TranslocoPipe } from '@jsverse/transloco';
import { PAGE_SIZE_ALL } from '../filtering/grid/page-size-all';
import { CatalogueFilter } from '../state/catalogue-filter';
import { PAGE_SIZES } from '../state/page-sizes';

/**
 * The page sizes of the list, the current one pressed. Shown in the head of the results and
 * again under a long grid, so the reader who reached its end changes it there.
 */
@Component({
  selector: 'lodb-page-size-picker',
  imports: [TranslocoPipe],
  template: `<span class="label">{{ 'filter.per_page' | transloco }}</span>
    <div class="segments" role="group" [attr.aria-label]="'filter.per_page' | transloco">
      @for (size of sizes; track size) {
        <button
          type="button"
          class="segment"
          [attr.aria-pressed]="filter.state().size === size"
          (click)="filter.setSize(size)"
        >
          {{ size === all ? ('filter.all' | transloco) : size }}
        </button>
      }
    </div>`,
  styleUrl: './page-size-picker.css',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class PageSizePicker {
  protected readonly filter = inject(CatalogueFilter);
  protected readonly sizes = PAGE_SIZES;
  protected readonly all = PAGE_SIZE_ALL;
}
