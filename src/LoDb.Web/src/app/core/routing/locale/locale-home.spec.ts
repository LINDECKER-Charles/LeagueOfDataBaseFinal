import { LocationStrategy } from '@angular/common';
import { Component } from '@angular/core';
import { TestBed } from '@angular/core/testing';
import { provideRouter, RouterLink } from '@angular/router';
import { LocaleHomeLocationStrategy } from './locale-home-location-strategy';
import { withHomeSlash } from './with-home-slash';

describe('withHomeSlash', () => {
  it.each([
    ['/fr', '/fr/'],
    ['/zh-hant?lang=zh_TW', '/zh-hant/?lang=zh_TW'],
    ['/en#top', '/en/#top'],
    ['/fr/', '/fr/'],
    ['/fr/champions', '/fr/champions'],
    ['/en/about?x=/fr', '/en/about?x=/fr'],
    ['/admin', '/admin'],
    ['/EN', '/EN'],
    ['/xx', '/xx'],
    ['/', '/'],
    ['', ''],
  ])('writes %j as %j', (url, expected) => {
    expect(withHomeSlash(url)).toBe(expected);
  });
});

@Component({
  imports: [RouterLink],
  template: `<a routerLink="/fr">home</a><a routerLink="/fr/champions">champions</a>`,
})
class Links {}

describe('LocaleHomeLocationStrategy', () => {
  it('gives the links to a home their slash, and no other link', async () => {
    TestBed.configureTestingModule({
      providers: [
        provideRouter([]),
        { provide: LocationStrategy, useClass: LocaleHomeLocationStrategy },
      ],
    });
    const fixture = TestBed.createComponent(Links);
    await fixture.whenStable();

    const links = (fixture.nativeElement as HTMLElement).querySelectorAll('a');
    expect(Array.from(links, (link) => link.getAttribute('href'))).toEqual([
      '/fr/',
      '/fr/champions',
    ]);
  });
});
