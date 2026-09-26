import { ChangeDetectionStrategy, Component, inject } from '@angular/core';
import { RouterLink } from '@angular/router';
import { provideTranslocoScope, TranslocoPipe } from '@jsverse/transloco';
import { PageDirection } from '../../../core/layout/direction/page-direction';
import { localePath } from '../../../core/layout/shell/locale-path';
import { injectRouteData } from '../../../core/routing/inject-route-data';
import { breadcrumbList } from '../../../core/seo/json-ld/site/breadcrumb-list';
import { faqPage } from '../../../core/seo/json-ld/site/faq-page';
import { applyEditorialHead } from '../shared/apply-editorial-head';
import { EditorialFrame } from '../shared/editorial-frame';
import { FAQ_ENTRIES } from './faq-entries';

/**
 * The questions people actually ask, answered from the `about` scope, with the FAQPage node
 * built from the same entries as the list.
 */
@Component({
  selector: 'lodb-faq-page',
  imports: [EditorialFrame, RouterLink, TranslocoPipe],
  providers: [provideTranslocoScope('about', 'api')],
  templateUrl: './faq-page.html',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class FaqPage {
  protected readonly heading = injectRouteData<string>('heading');
  protected readonly entries = FAQ_ENTRIES;
  private readonly locale = inject(PageDirection).locale;

  constructor() {
    applyEditorialHead(['about', 'seo'], (translate) => ({
      title: translate('seo.about.faq.title'),
      description: translate('seo.about.faq.description'),
      path: 'faq',
      jsonLd: (urls) => [
        breadcrumbList([
          { name: translate('header.navigation.home'), url: urls.page('') },
          { name: translate('about.index.title'), url: urls.page('about') },
          { name: translate('about.faq.title'), url: urls.canonical },
        ]),
        faqPage(
          FAQ_ENTRIES.map((id) => ({
            question: translate(`about.faq.${id}.question`),
            answer: translate(`about.faq.${id}.answer`),
          })),
          urls.canonical,
        ),
      ],
    }));
  }

  protected link(path: string): string {
    return localePath(this.locale(), path);
  }
}
