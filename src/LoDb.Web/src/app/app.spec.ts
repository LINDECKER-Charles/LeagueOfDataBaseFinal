import { TestBed } from '@angular/core/testing';
import { provideRouter } from '@angular/router';
import { provideTransloco } from '@jsverse/transloco';
import { of } from 'rxjs';
import { App } from './app';

// The slots of the shell (plan, section 5.2), each with the component a chantier replaces.
describe('App', () => {
  let lang: string;

  async function render(): Promise<HTMLElement> {
    TestBed.configureTestingModule({
      providers: [
        provideRouter([]),
        provideTransloco({
          config: { defaultLang: 'en', missingHandler: { logMissingKey: false }, prodMode: true },
          loader: class {
            getTranslation = () => of({});
          },
        }),
      ],
    });
    const fixture = TestBed.createComponent(App);
    await fixture.whenStable();
    return fixture.nativeElement as HTMLElement;
  }

  beforeEach(() => {
    lang = document.documentElement.lang;
  });

  afterEach(() => {
    document.documentElement.lang = lang;
    document.documentElement.removeAttribute('dir');
  });

  it('puts the account menu and the context switcher in the header', async () => {
    const header = (await render()).querySelector('lodb-shell header');

    expect(header?.querySelector('lodb-account-menu')).not.toBeNull();
    expect(header?.querySelector('lodb-context-switcher')).not.toBeNull();
  });

  it('puts both banners between the header and the page', async () => {
    const shell = (await render()).querySelector('lodb-shell');
    const children = Array.from(shell?.children ?? [], (child) => child.tagName.toLowerCase());
    const header = children.indexOf('lodb-header');

    expect(header).toBeGreaterThanOrEqual(0);
    expect(children.slice(header + 1, header + 4)).toEqual([
      'lodb-verify-email-banner',
      'lodb-update-banner',
      'main',
    ]);
  });

  it('renders the routed page in main', async () => {
    const main = (await render()).querySelector('lodb-shell > main');

    expect(main?.querySelector('router-outlet')).not.toBeNull();
  });

  it('puts the contact entry in the footer', async () => {
    const footer = (await render()).querySelector('lodb-shell footer');

    expect(footer?.querySelector('lodb-contact-dialog')).not.toBeNull();
  });

  it('draws the update screen over the whole shell, after it', async () => {
    const shell = (await render()).querySelector('lodb-shell');

    expect(shell?.nextElementSibling?.tagName.toLowerCase()).toBe('lodb-update-screen');
  });
});
