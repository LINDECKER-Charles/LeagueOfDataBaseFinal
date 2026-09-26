import { Directionality } from '@angular/cdk/bidi';
import { TestBed } from '@angular/core/testing';
import { type Event, NavigationEnd, NavigationStart, Router } from '@angular/router';
import { Subject } from 'rxjs';
import { LOCALES } from '../../i18n/locales';
import { PageDirection } from './page-direction';
import { textDirection } from './text-direction';

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
  let events: Subject<Event>;

  function pageDirection(lang: string): PageDirection {
    document.documentElement.lang = lang;
    events = new Subject<Event>();
    TestBed.configureTestingModule({
      providers: [{ provide: Router, useValue: { events } }],
    });
    return TestBed.inject(PageDirection);
  }

  // The locale resolver writes <html lang> during the navigation, before it ends.
  function navigateTo(lang: string): void {
    document.documentElement.lang = lang;
    events.next(new NavigationEnd(1, `/${lang}`, `/${lang}`));
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

  it('follows the locale of each completed navigation and tells the CDK once', () => {
    const page = pageDirection('fr');
    const changes: string[] = [];
    TestBed.inject(Directionality).change.subscribe((direction) => changes.push(direction));

    navigateTo('ar');
    navigateTo('ar');
    navigateTo('de');

    expect(page.locale()).toBe('de');
    expect(document.documentElement.dir).toBe('ltr');
    expect(changes).toEqual(['rtl', 'ltr']);
  });

  it('waits for the navigation to end', () => {
    const page = pageDirection('en');

    document.documentElement.lang = 'ar';
    events.next(new NavigationStart(1, '/ar'));

    expect(page.locale()).toBe('en');
    expect(document.documentElement.dir).toBe('ltr');
  });
});
