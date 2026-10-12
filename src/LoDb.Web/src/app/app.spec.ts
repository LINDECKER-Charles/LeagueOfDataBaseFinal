import { TestBed } from '@angular/core/testing';
import { provideRouter } from '@angular/router';
import { App } from './app';
import { provideTestTransloco } from './core/testing/provide-test-transloco';

// The slots of the shell (plan, section 5.2), each with the component a chantier replaces.
describe('App', () => {
  let lang: string;

  beforeEach(() => {
    lang = document.documentElement.lang;
  });

  afterEach(() => {
    document.documentElement.lang = lang;
    document.documentElement.removeAttribute('dir');
  });

  it('places each slot of the shell, the update screen over all of it', async () => {
    TestBed.configureTestingModule({ providers: [provideRouter([]), provideTestTransloco()] });
    const fixture = TestBed.createComponent(App);
    await fixture.whenStable();
    const shell = (fixture.nativeElement as HTMLElement).querySelector('lodb-shell');
    const children = Array.from(shell?.children ?? [], (child) => child.tagName.toLowerCase());
    const header = children.indexOf('lodb-header');

    expect(shell?.querySelector('header lodb-account-menu')).not.toBeNull();
    expect(shell?.querySelector('header lodb-context-switcher')).not.toBeNull();
    // Both banners sit between the header and the page.
    expect(header).toBeGreaterThanOrEqual(0);
    expect(children.slice(header + 1, header + 4)).toEqual([
      'lodb-verify-email-banner',
      'lodb-update-banner',
      'main',
    ]);
    expect(shell?.querySelector(':scope > main router-outlet')).not.toBeNull();
    expect(shell?.querySelector('footer lodb-contact-dialog')).not.toBeNull();
    expect(shell?.nextElementSibling?.tagName.toLowerCase()).toBe('lodb-update-screen');
  });
});
