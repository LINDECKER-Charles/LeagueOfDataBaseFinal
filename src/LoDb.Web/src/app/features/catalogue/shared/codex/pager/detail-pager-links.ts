import type { UrlTree } from '@angular/router';
import type { PagerLink } from '../../../../../ui/navigation/pager-link';

/** What lodb-pager draws under a detail page. */
export interface DetailPagerLinks {
  readonly previous: PagerLink | null;
  readonly next: PagerLink | null;
  /** The list the entry belongs to. */
  readonly hub: UrlTree;
}
