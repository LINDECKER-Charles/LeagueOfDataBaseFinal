import { ChangeDetectionStrategy, Component } from '@angular/core';
import { provideTranslocoScope } from '@jsverse/transloco';
import type { RuneTreeDetails } from '../../../core/api/generated/models/rune-tree-details';
import type { CatalogueEntry } from '../../../core/routing/catalogue/catalogue-entry';
import { injectRouteData } from '../../../core/routing/inject-route-data';
import { RuneDetail } from './detail/rune-detail';
import { RuneList } from './list/rune-list';

/**
 * The rune pages (runes.routes.ts): the rune path a route resolved, else the list of every
 * rune. Both read the feature's own texts, `public/i18n/runes/`.
 */
@Component({
  selector: 'lodb-runes-page',
  imports: [RuneDetail, RuneList],
  providers: [provideTranslocoScope('runes')],
  template: `@if (entry()) {
      <lodb-rune-detail />
    } @else {
      <lodb-rune-list />
    }`,
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class RunesPage {
  /** The rune path of a detail page, resolved by `resolveCatalogueEntry`. */
  protected readonly entry = injectRouteData<CatalogueEntry<RuneTreeDetails> | undefined>('entry');
}
