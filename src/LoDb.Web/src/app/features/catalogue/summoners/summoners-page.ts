import { ChangeDetectionStrategy, Component } from '@angular/core';
import { provideTranslocoScope } from '@jsverse/transloco';
import type { SummonerDetails } from '../../../core/api/generated/models/summoner-details';
import type { CatalogueEntry } from '../../../core/routing/catalogue/catalogue-entry';
import { injectRouteData } from '../../../core/routing/inject-route-data';
import { SummonerDetail } from './detail/summoner-detail';
import { SummonerList } from './list/summoner-list';

/**
 * The summoner spell pages (summoners.routes.ts): the spell a route resolved, else the list.
 * Both read the feature's own texts, `public/i18n/summoners/`.
 */
@Component({
  selector: 'lodb-summoners-page',
  imports: [SummonerDetail, SummonerList],
  providers: [provideTranslocoScope('summoners')],
  template: `@if (entry()) {
      <lodb-summoner-detail />
    } @else {
      <lodb-summoner-list />
    }`,
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class SummonersPage {
  /** The spell of a detail page, resolved by `resolveCatalogueEntry`. */
  protected readonly entry = injectRouteData<CatalogueEntry<SummonerDetails> | undefined>('entry');
}
