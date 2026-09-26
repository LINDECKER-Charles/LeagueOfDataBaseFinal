import { Directionality } from '@angular/cdk/bidi';
import { TestBed } from '@angular/core/testing';
import { TranslocoService, type TranslocoEvents } from '@jsverse/transloco';
import { Subject } from 'rxjs';
import { LOCALES } from '../../i18n/locales';
import { PageDirection } from './page-direction';
import { textDirection } from './text-direction';

function languageChange(langName: string): TranslocoEvents {
  return { type: 'langChanged', payload: { langName, scope: null } };
}

describe('textDirection', () => {
  it('reads Arabic right to left', () => {
    expect(textDirection('ar')).toBe('rtl');
  });

  it('reads every other locale left to right', () => {
    const others = LOCALES.filter((locale) => locale !== 'ar');

    expect(others.map(textDirection)).toEqual(others.map(() => 'ltr'));
  });
});

describe('PageDirection', () => {
  let events: Subject<TranslocoEvents>;

  function pageDirection(lang: string): PageDirection {
    document.documentElement.lang = lang;
    events = new Subject<TranslocoEvents>();
    TestBed.configureTestingModule({
      providers: [{ provide: TranslocoService, useValue: { events$: events } }],
    });
    return TestBed.inject(PageDirection);
  }

  afterEach(() => {
    document.documentElement.lang = 'en';
    document.documentElement.removeAttribute('dir');
  });

  it('starts from the SSR <html lang>, so an Arabic page never flashes left to right', () => {
    const page = pageDirection('ar');

    expect(page.locale()).toBe('ar');
    expect(page.direction()).toBe('rtl');
    expect(document.documentElement.dir).toBe('rtl');
    expect(TestBed.inject(Directionality).value).toBe('rtl');
  });

  it('starts from the default locale when <html lang> is not a site locale', () => {
    const page = pageDirection('xx');

    expect(page.locale()).toBe('en');
    expect(document.documentElement.dir).toBe('ltr');
  });

  it('follows a language change and tells the CDK once', () => {
    const page = pageDirection('fr');
    const changes: string[] = [];
    TestBed.inject(Directionality).change.subscribe((direction) => changes.push(direction));

    events.next(languageChange('ar'));
    events.next(languageChange('ar'));
    events.next(languageChange('de'));

    expect(page.locale()).toBe('de');
    expect(document.documentElement.dir).toBe('ltr');
    expect(changes).toEqual(['rtl', 'ltr']);
  });

  it('ignores other events and languages outside the site locales', () => {
    const page = pageDirection('en');

    events.next({
      type: 'translationLoadSuccess',
      wasFailure: false,
      payload: { langName: 'ar', scope: null },
    });
    events.next(languageChange('he'));

    expect(page.locale()).toBe('en');
    expect(document.documentElement.dir).toBe('ltr');
  });
});
