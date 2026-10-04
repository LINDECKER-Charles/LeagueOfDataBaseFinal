import { PLATFORM_ID, REQUEST } from '@angular/core';
import { TestBed } from '@angular/core/testing';
import { CANONICAL_ORIGIN } from './canonical-origin';

function originOn(platform: 'browser' | 'server', request?: Request): string {
  TestBed.configureTestingModule({
    providers: [
      { provide: PLATFORM_ID, useValue: platform },
      ...(request ? [{ provide: REQUEST, useValue: request }] : []),
    ],
  });
  return TestBed.inject(CANONICAL_ORIGIN);
}

describe('CANONICAL_ORIGIN', () => {
  it('is the origin of the request an SSR render answers', () => {
    const request = new Request('https://league-of-data-base.com/fr/champions?page=2');

    expect(originOn('server', request)).toBe('https://league-of-data-base.com');
  });

  it('is the origin the browser was served from', () => {
    expect(originOn('browser')).toBe(document.location.origin);
  });

  it("is production's when a page renders without a request", () => {
    expect(originOn('server')).toBe('https://league-of-data-base.com');
  });
});
