import { type Provider } from '@angular/core';
import { TestBed } from '@angular/core/testing';
import { provideRouter } from '@angular/router';
import { provideTransloco } from '@jsverse/transloco';
import { of } from 'rxjs';
import { PAYMENTS_ENABLED } from '../../../../environments/payments-enabled';
import type { AccountUser } from '../../api/generated/models/account-user';
import { AuthSession } from '../../auth/session/auth-session';
import { AUTH_STRATEGY } from '../../auth/strategy/auth-strategy-token';
import { accountUser } from '../../auth/testing/account-user';
import { FakeAuthStrategy } from '../../auth/testing/fake-auth-strategy';
import { PLATFORM } from '../../platform/platform';
import type { PlatformKind } from '../../platform/platform-kind';
import { Footer } from './footer';

interface Setup {
  readonly payments?: boolean;
  /** The detected platform and who is signed in; none by default, as in the shell's specs. */
  readonly platform?: { readonly kind: PlatformKind; readonly user: AccountUser | null };
}

function platformProviders(platform: Setup['platform']): Provider[] {
  if (platform === undefined) {
    return [];
  }
  const strategy = new FakeAuthStrategy();
  strategy.sessionAnswer = of({ user: platform.user });
  return [
    { provide: PLATFORM, useValue: { kind: platform.kind } },
    { provide: AUTH_STRATEGY, useValue: strategy },
  ];
}

// The footer as rendered, once the session (if any) is read.
async function footer({ payments = true, platform }: Setup = {}): Promise<HTMLElement> {
  TestBed.configureTestingModule({
    providers: [
      provideRouter([]),
      provideTransloco({
        config: { defaultLang: 'en', missingHandler: { logMissingKey: false }, prodMode: true },
        loader: class {
          getTranslation = () => of({});
        },
      }),
      { provide: PAYMENTS_ENABLED, useValue: payments },
      platformProviders(platform),
    ],
  });
  if (platform !== undefined) {
    await TestBed.inject(AuthSession).load();
  }
  const fixture = TestBed.createComponent(Footer);
  await fixture.whenStable();
  return fixture.nativeElement as HTMLElement;
}

// The site map of the footer, as the hrefs it renders.
function siteMap(host: HTMLElement): (string | null)[] {
  return Array.from(host.querySelectorAll('nav a'), (link) => link.getAttribute('href'));
}

describe('Footer', () => {
  it('lists the donation page in a build with payments', async () => {
    expect(siteMap(await footer())).toContain('/en/donate');
  });

  it('leaves the donation page out of the store build, which has no payment', async () => {
    const links = siteMap(await footer({ payments: false }));

    expect(links).not.toContain('/en/donate');
    expect(links).toContain('/en/developers');
  });

  it('ends with the XML site map on the web, outside the locale', async () => {
    const web = siteMap(await footer({ platform: { kind: 'web', user: null } }));

    expect(web.at(-1)).toBe('/sitemap.xml');
  });

  it('leaves the site map out of the apps, whose origin serves none', async () => {
    expect(siteMap(await footer({ platform: { kind: 'android', user: null } }))).not.toContain(
      '/sitemap.xml',
    );
  });

  it("lists a member's profile before the site map, and a visitor's none", async () => {
    const member = siteMap(await footer({ platform: { kind: 'web', user: accountUser() } }));
    TestBed.resetTestingModule();
    const visitor = siteMap(await footer({ platform: { kind: 'web', user: null } }));

    expect(member.slice(-2)).toEqual(['/en/account/profile', '/sitemap.xml']);
    expect(visitor).not.toContain('/en/account/profile');
  });

  it('opens the outside links in a tab, and the e-mail in the mail client', async () => {
    const host = await footer();
    const mail = host.querySelector('a[href^="mailto:"]');
    const github = host.querySelector('a[href^="https://github.com"]');

    expect(mail?.hasAttribute('target')).toBe(false);
    expect(github?.getAttribute('target')).toBe('_blank');
    expect(github?.getAttribute('rel')).toBe('noopener noreferrer');
  });
});
