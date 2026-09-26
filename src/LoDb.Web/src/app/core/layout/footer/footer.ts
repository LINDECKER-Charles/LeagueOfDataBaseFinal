import { ChangeDetectionStrategy, Component, inject } from '@angular/core';
import { RouterLink } from '@angular/router';
import { provideTranslocoScope, TranslocoPipe } from '@jsverse/transloco';
import { Icon } from '../../../ui/media/icon';
import { Logo } from '../../../ui/media/logo';
import { PageDirection } from '../direction/page-direction';
import { localePath } from '../shell/locale-path';
import { RELEASE_VERSION } from '../shell/release-version';
import { CONTACT_LINKS } from './contact-links';
import { EXTERNAL_LINKS } from './external-links';
import { LEGAL_LINKS } from './legal-links';
import { SITE_LINKS } from './site-links';

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
  protected readonly site = SITE_LINKS;
  protected readonly legal = LEGAL_LINKS;
  protected readonly external = EXTERNAL_LINKS;
  protected readonly contact = CONTACT_LINKS;
  protected readonly version = inject(RELEASE_VERSION);
  private readonly page = inject(PageDirection);

  protected link(path: string): string {
    return localePath(this.page.locale(), path);
  }
}
