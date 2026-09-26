import { ChangeDetectionStrategy, Component, inject } from '@angular/core';
import { RouterLink } from '@angular/router';
import { provideTranslocoScope, TranslocoPipe } from '@jsverse/transloco';
import { PAYMENTS_ENABLED } from '../../../../environments/payments-enabled';
import { Icon } from '../../../ui/media/icon';
import { Logo } from '../../../ui/media/logo';
import { PageDirection } from '../direction/page-direction';
import { localePath } from '../shell/locale-path';
import { RELEASE_VERSION } from '../shell/release-version';
import { CONTACT_LINKS } from './contact-links';
import { EXTERNAL_LINKS } from './external-links';
import { LEGAL_LINKS } from './legal-links';
import { SITE_LINKS } from './site-links';

const DONATE_PATH = 'donate';

/**
 * Site footer: brand and copyright, site map, outside links, release, legal pages, contact
 * links with the `contact` slot under them, then Riot's legal disclaimer, which Riot's
 * policy words in English and is kept verbatim in every locale. The site map labels some
 * pages from the `about` and `api` catalogue scopes, loaded here since the footer sits
 * outside the pages that provide them.
 */
@Component({
  selector: 'lodb-footer',
  imports: [Icon, Logo, RouterLink, TranslocoPipe],
  providers: [provideTranslocoScope('about', 'api')],
  templateUrl: './footer.html',
  host: { class: 'block' },
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class Footer {
  // A build without payments (the store app, ADR 0007) does not route the donation page.
  protected readonly site = inject(PAYMENTS_ENABLED)
    ? SITE_LINKS
    : SITE_LINKS.filter((entry) => entry.path !== DONATE_PATH);
  protected readonly legal = LEGAL_LINKS;
  protected readonly external = EXTERNAL_LINKS;
  protected readonly contact = CONTACT_LINKS;
  protected readonly version = inject(RELEASE_VERSION);
  private readonly page = inject(PageDirection);

  protected link(path: string): string {
    return localePath(this.page.locale(), path);
  }
}
