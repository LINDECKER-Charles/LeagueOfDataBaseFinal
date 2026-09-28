import { ChangeDetectionStrategy, Component, inject } from '@angular/core';
import { RouterLink } from '@angular/router';
import { provideTranslocoScope, TranslocoPipe } from '@jsverse/transloco';
import { PageDirection } from '../../../core/layout/direction/page-direction';
import { localePath } from '../../../core/layout/shell/locale-path';
import { injectRouteData } from '../../../core/routing/inject-route-data';
import { aboutPage } from '../../../core/seo/json-ld/site/about-page';
import { breadcrumbList } from '../../../core/seo/json-ld/site/breadcrumb-list';
import { applyEditorialHead } from '../shared/apply-editorial-head';
import { EditorialFrame } from '../shared/editorial-frame';
import { ABOUT_FEATURES } from './about-features';
import { InventoryCounters } from './inventory/inventory-counters';
import type { InventoryFact } from './inventory/inventory-fact';
import { InventoryFacts } from './inventory/inventory-facts';

/**
 * What the project is, who it is for and what it offers, in prose a search or answer engine
 * can quote. Prerendered: its counters load in the browser.
 */
@Component({
  selector: 'lodb-about-page',
  imports: [EditorialFrame, InventoryCounters, InventoryFacts, RouterLink, TranslocoPipe],
  providers: [provideTranslocoScope('about', 'api')],
  templateUrl: './about-page.html',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class AboutPage {
  protected readonly heading = injectRouteData<string>('heading');
  protected readonly features = ABOUT_FEATURES;
  protected readonly facts: readonly InventoryFact[] = ['champions', 'items', 'runes', 'summoners'];
  private readonly locale = inject(PageDirection).locale;

  constructor() {
    applyEditorialHead(['about', 'seo'], (translate, locale) => {
      const name = translate('about.index.title');
      const description = translate('seo.about.description');
      return {
        title: translate('seo.about.title'),
        description,
        path: 'about',
        jsonLd: (urls) => [
          breadcrumbList([
            { name: translate('header.navigation.home'), url: urls.page('') },
            { name, url: urls.canonical },
          ]),
          aboutPage({ name, url: urls.canonical, description, inLanguage: locale }),
        ],
      };
    });
  }

  protected link(path: string): string {
    return localePath(this.locale(), path);
  }
}
