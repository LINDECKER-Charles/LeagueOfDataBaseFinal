import { ChangeDetectionStrategy, Component, computed, input } from '@angular/core';
import { RouterLink } from '@angular/router';
import { TranslocoPipe } from '@jsverse/transloco';
import { pageParamsOf } from '../filters/trends-params';

/**
 * The pages of the trends: the previous and next ones around "page n / pages", each link
 * keeping the filters of the query and carrying `rel`. Hidden when the list fits one page.
 */
@Component({
  selector: 'lodb-trends-pager',
  imports: [RouterLink, TranslocoPipe],
  templateUrl: './trends-pager.html',
  styleUrl: './trends-pager.css',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class TrendsPager {
  readonly page = input.required<number>();
  readonly pages = input.required<number>();

  protected readonly previous = computed(() =>
    this.page() > 1 ? pageParamsOf(this.page() - 1) : null,
  );
  protected readonly next = computed(() =>
    this.page() < this.pages() ? pageParamsOf(this.page() + 1) : null,
  );
}
