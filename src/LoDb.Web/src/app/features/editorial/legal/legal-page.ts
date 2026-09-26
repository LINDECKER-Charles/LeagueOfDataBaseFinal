import { ChangeDetectionStrategy, Component, computed, inject } from '@angular/core';
import { TranslocoPipe } from '@jsverse/transloco';
import { PageDirection } from '../../../core/layout/direction/page-direction';
import { injectRouteData } from '../../../core/routing/inject-route-data';
import { SectionNav } from '../../../ui/navigation/section-nav';
import { applyEditorialHead } from '../shared/apply-editorial-head';
import { EditorialFrame } from '../shared/editorial-frame';
import { LEGAL_CONTENTS } from './legal-contents';
import { LEGAL_INFO } from './legal-info-token';
import { legalLanguageOf } from './legal-language-of';
import type { LegalPageId } from './legal-page-id';
import { CookiesEn } from './texts/cookies-en';
import { CookiesFr } from './texts/cookies-fr';
import { NoticeEn } from './texts/notice-en';
import { NoticeFr } from './texts/notice-fr';
import { PrivacyEn } from './texts/privacy-en';
import { PrivacyFr } from './texts/privacy-fr';
import { TermsEn } from './texts/terms-en';
import { TermsFr } from './texts/terms-fr';

/**
 * A legal page (notice, privacy, terms, cookies), named by its route. Its chrome speaks the
 * page locale; its text is the French one under a `fr*` locale and the English one under any
 * other, marked with its own language when it differs from the page's.
 */
@Component({
  selector: 'lodb-legal-page',
  imports: [
    CookiesEn,
    CookiesFr,
    EditorialFrame,
    NoticeEn,
    NoticeFr,
    PrivacyEn,
    PrivacyFr,
    SectionNav,
    TermsEn,
    TermsFr,
    TranslocoPipe,
  ],
  templateUrl: './legal-page.html',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class LegalPage {
  protected readonly heading = injectRouteData<string>('heading');
  protected readonly page = injectRouteData<LegalPageId>('legalPage');
  protected readonly info = inject(LEGAL_INFO);
  protected readonly locale = inject(PageDirection).locale;
  protected readonly language = computed(() => legalLanguageOf(this.locale()));
  protected readonly textLanguage = computed(() =>
    this.language() === this.locale() ? null : this.language(),
  );
  protected readonly contents = computed(() => LEGAL_CONTENTS[this.page()][this.language()]);

  constructor() {
    applyEditorialHead(['seo'], (translate) => ({
      title: translate(this.heading()),
      description: translate(`seo.legal.${this.page()}.description`),
      path: `legal/${this.page()}`,
    }));
  }
}
