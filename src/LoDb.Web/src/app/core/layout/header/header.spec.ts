import { TestBed } from '@angular/core/testing';
import { provideRouter } from '@angular/router';
import { provideTransloco } from '@jsverse/transloco';
import { of } from 'rxjs';
import { PAYMENTS_ENABLED } from '../../../../environments/payments-enabled';
import { Header } from './header';

// Every link of the header, as the hrefs it renders.
async function hrefs(payments: boolean): Promise<(string | null)[]> {
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
  const fixture = TestBed.createComponent(Header);
  await fixture.whenStable();
  const links = (fixture.nativeElement as HTMLElement).querySelectorAll('a');
  return Array.from(links, (link) => link.getAttribute('href'));
}

describe('Header', () => {
  it('links the donation page in a build with payments', async () => {
    expect(await hrefs(true)).toContain('/en/donate');
  });

  it('shows no donation link in the store build, which has no payment (ADR 0007)', async () => {
    const links = await hrefs(false);

    expect(links).not.toContain('/en/donate');
    expect(links).toContain('/en/developers');
  });
});
