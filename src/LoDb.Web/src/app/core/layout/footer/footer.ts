import { ChangeDetectionStrategy, Component, computed, inject } from '@angular/core';
import { RouterLink } from '@angular/router';
import { provideTranslocoScope, TranslocoPipe } from '@jsverse/transloco';
import { PAYMENTS_ENABLED } from '../../../../environments/payments-enabled';
import { Icon } from '../../../ui/media/icon';
import { Logo } from '../../../ui/media/logo';
import { AuthSession } from '../../auth/session/auth-session';
import { PLATFORM } from '../../platform/platform';
import { PageDirection } from '../direction/page-direction';
import { injectChromeLinks } from '../nav/inject-chrome-links';
import { localePath } from '../shell/locale-path';
import { RELEASE_VERSION } from '../shell/release-version';
import { CONTACT_LINKS } from './contact-links';
import { EXTERNAL_LINKS } from './external-links';
import type { FooterLink } from './footer-link';
import { LEGAL_LINKS } from './legal-links';
import { SITE_LINKS } from './site-links';

const DONATE_PATH = 'donate';
// Served by the site's own origin (nginx), outside the router: the apps' shells have none.
const SITEMAP_URL = '/sitemap.xml';
const MAIL_SCHEME = 'mailto:';

/**
 * Site footer: brand and copyright, site map, outside links, release, legal pages, contact
 * links with the `contact` slot under them, then Riot's legal disclaimer, which Riot's
 * policy words in English and is kept verbatim in every locale. The site map labels some
 * pages from the `about` and `api` catalogue scopes, loaded here since the footer sits
 * outside the pages that provide them. Its catalogue links keep the page's version and variant.
 * A signed-in member also finds their profile there; the session is read in the browser only,
 * so the server renders what a visitor sees. Outside links open a tab, the e-mail one does not.
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
  protected readonly site = injectChromeLinks(
    inject(PAYMENTS_ENABLED)
      ? SITE_LINKS
      : SITE_LINKS.filter((entry) => entry.path !== DONATE_PATH),
  );
  protected readonly legal = LEGAL_LINKS;
  protected readonly external = EXTERNAL_LINKS;
  protected readonly contact = CONTACT_LINKS;
  protected readonly version = inject(RELEASE_VERSION);
  private readonly page = inject(PageDirection);
  private readonly platform = inject(PLATFORM, { optional: true });
  // An application that never detected its platform (the shell's specs) has no session.
  private readonly session = this.platform === null ? null : inject(AuthSession);
  protected readonly member = computed(() => (this.session?.user() ?? null) !== null);
  protected readonly sitemap = (this.platform?.kind ?? 'web') === 'web' ? SITEMAP_URL : null;

  protected link(path: string): string {
    return localePath(this.page.locale(), path);
  }

  protected opensTab(entry: FooterLink): boolean {
    return !entry.href.startsWith(MAIL_SCHEME);
  }
}
