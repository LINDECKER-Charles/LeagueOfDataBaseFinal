import { ActivatedRouteSnapshot, type Route, UrlSegment } from '@angular/router';
import { localeGuard } from './locale-guard';
import type { Locale } from './locales';

describe('localeGuard', () => {
  const route: Route = { path: ':locale' };
  const matches = (...paths: string[]) =>
    localeGuard(
      route,
      paths.map((path) => new UrlSegment(path, {})),
      new ActivatedRouteSnapshot(),
    );

  // Every locale goes through the same lookup: a plain one, and one with a script subtag.
  it.each<Locale>(['en', 'zh-hant'])('accepts %s', (locale) => {
    expect(matches(locale)).toBe(true);
    expect(matches(locale, 'champions', '42')).toBe(true);
  });

  // Wrong case, a truncated or misspelled locale, a region form, an unknown language, a
  // reserved first segment, an empty one.
  it.each(['EN', 'zh', 'zh_hans', 'pt-br', 'xx', 'api', ''])('refuses %j', (segment) => {
    expect(matches(segment)).toBe(false);
  });

  it('refuses an empty URL, left to the root redirect', () => {
    expect(matches()).toBe(false);
  });
});
