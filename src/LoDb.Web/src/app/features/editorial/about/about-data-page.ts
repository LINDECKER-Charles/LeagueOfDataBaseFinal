import { ChangeDetectionStrategy, Component, inject } from '@angular/core';
import { RouterLink } from '@angular/router';
import { provideTranslocoScope, TranslocoPipe } from '@jsverse/transloco';
import { PageDirection } from '../../../core/layout/direction/page-direction';
import { localePath } from '../../../core/layout/shell/locale-path';
import { injectRouteData } from '../../../core/routing/inject-route-data';
import { breadcrumbList } from '../../../core/seo/json-ld/site/breadcrumb-list';
import { dataset } from '../../../core/seo/json-ld/site/dataset';
import { applyEditorialHead } from '../shared/apply-editorial-head';
import { EditorialFrame } from '../shared/editorial-frame';
import { DATA_DRAGON_LANGUAGES } from './data-dragon-languages';
import { InventoryCounters } from './inventory/inventory-counters';
import type { InventoryFact } from './inventory/inventory-fact';
import { InventoryFacts } from './inventory/inventory-facts';

const DATASET_KEYWORDS = [
  'League of Legends',
  'Data Dragon',
  'champions',
  'items',
  'runes',
  'summoner spells',
];

/**
 * Where the data comes from, how fresh it is and what it covers, with its Dataset node.
 * Prerendered: the patch and the counts load in the browser, so neither the description nor
 * the Dataset node names a patch (the current site's did).
 */
@Component({
  selector: 'lodb-about-data-page',
  imports: [EditorialFrame, InventoryCounters, InventoryFacts, RouterLink, TranslocoPipe],
  providers: [provideTranslocoScope('about', 'editorial')],
  templateUrl: './about-data-page.html',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class AboutDataPage {
  protected readonly heading = injectRouteData<string>('heading');
  protected readonly facts: readonly InventoryFact[] = [
    'version',
    'champions',
    'items',
    'summoners',
    'languages',
    'versions',
  ];
  private readonly locale = inject(PageDirection).locale;

  constructor() {
    applyEditorialHead(['about', 'seo', 'editorial'], (translate) => {
      const description = translate('editorial.data.description');
      return {
        title: translate('seo.about.data.title'),
        description,
        path: 'about/data',
        image: '/preview/items.png',
        jsonLd: (urls) => [
          breadcrumbList([
            { name: translate('header.navigation.home'), url: urls.page('') },
            { name: translate('about.index.title'), url: urls.page('about') },
            { name: translate('about.data.title'), url: urls.canonical },
          ]),
          dataset({
            name: translate('editorial.data.dataset_name'),
            url: urls.canonical,
            description,
            languages: DATA_DRAGON_LANGUAGES,
            keywords: DATASET_KEYWORDS,
            creatorId: `${urls.origin}/#organization`,
          }),
        ],
      };
    });
  }

  protected link(path: string): string {
    return localePath(this.locale(), path);
  }
}
