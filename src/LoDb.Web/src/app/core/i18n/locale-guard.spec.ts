import { ActivatedRouteSnapshot, type Route, UrlSegment } from '@angular/router';
import { localeGuard } from './locale-guard';
import { LOCALES } from './locales';

describe('localeGuard', () => {
  const route: Route = { path: ':locale' };
  const matches = (...paths: string[]) =>
    localeGuard(
      route,
      paths.map((path) => new UrlSegment(path, {})),
      new ActivatedRouteSnapshot(),
    );

  it('declares the 21 locales of ADR 0005', () => {
    expect(LOCALES).toHaveLength(21);
    expect(new Set(LOCALES).size).toBe(21);
  });

  it.each(LOCALES)('accepts %s', (locale) => {
    expect(matches(locale)).toBe(true);
    expect(matches(locale, 'champions', '42')).toBe(true);
  });

  it.each([
    'EN',
    'Fr',
    'zh',
    'zh-Hans',
    'zh_hans',
    'zh-tw',
    'pt-br',
    'en-us',
    'nl',
    'xx',
    'i18n',
    'build',
    'api',
    '',
  ])('refuses %j', (segment) => {
    expect(matches(segment)).toBe(false);
  });

  it('refuses an empty URL, left to the root redirect', () => {
    expect(matches()).toBe(false);
  });
});
