import { TestBed } from '@angular/core/testing';
import { provideRouter } from '@angular/router';
import { provideTransloco } from '@jsverse/transloco';
import { of } from 'rxjs';
import { PAYMENTS_ENABLED } from '../../../../environments/payments-enabled';
import { Footer } from './footer';

// The site map of the footer, as the hrefs it renders.
async function siteMap(payments: boolean): Promise<(string | null)[]> {
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
    ],
  });
  const fixture = TestBed.createComponent(Footer);
  await fixture.whenStable();
  const links = (fixture.nativeElement as HTMLElement).querySelectorAll('nav a');
  return Array.from(links, (link) => link.getAttribute('href'));
}

describe('Footer', () => {
  it('lists the donation page in a build with payments', async () => {
    expect(await siteMap(true)).toContain('/en/donate');
  });

  it('leaves the donation page out of the store build, which has no payment', async () => {
    const links = await siteMap(false);

    expect(links).not.toContain('/en/donate');
    expect(links).toContain('/en/developers');
  });
});
