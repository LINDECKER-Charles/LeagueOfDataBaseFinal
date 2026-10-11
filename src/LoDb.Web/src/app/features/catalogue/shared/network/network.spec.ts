import { RESPONSE_INIT } from '@angular/core';
import { TestBed } from '@angular/core/testing';
import type { ImageVariant } from '../../../../core/api/generated/models/image-variant';
import { connectionOf } from './connection-of';
import { injectArtPreconnect } from './inject-art-preconnect';
import { wantsLightData } from './wants-light-data';

describe('wantsLightData', () => {
  it.each([
    [{ saveData: true, effectiveType: '4g' }, true],
    [{ effectiveType: 'slow-2g' }, true],
    [{ effectiveType: '2g' }, true],
    [{ effectiveType: '3g' }, true],
    [{ saveData: false, effectiveType: '4g' }, false],
    [{}, false],
    [undefined, false],
  ])('reads %o as %s', (connection, expected) => {
    expect(wantsLightData(connection)).toBe(expected);
  });
});

describe('connectionOf', () => {
  it('reads the hints of the navigator, when the browser gives them', () => {
    const connection = { saveData: true };
    const view = { navigator: { connection } } as unknown as Window;

    expect(connectionOf(view)).toBe(connection);
    expect(connectionOf({ navigator: {} } as unknown as Window)).toBeUndefined();
    expect(connectionOf(null)).toBeUndefined();
  });
});

describe('injectArtPreconnect', () => {
  function preconnectsOf(art: string, variants: readonly ImageVariant[]): string | null {
    const init: ResponseInit = { headers: new Headers() };
    TestBed.configureTestingModule({ providers: [{ provide: RESPONSE_INIT, useValue: init }] });
    TestBed.runInInjectionContext(injectArtPreconnect)(art, variants);
    TestBed.resetTestingModule();
    return new Headers(init.headers).get('Link');
  }

  it('connects early to the CDN of a hotlinked art', () => {
    expect(preconnectsOf('https://ddragon.leagueoflegends.com/cdn/img/a.jpg', [])).toBe(
      '<https://ddragon.leagueoflegends.com>; rel=preconnect',
    );
  });

  it('leaves the CDN alone when the site stores the art, or for a relative URL', () => {
    const stored = [{ url: '/cdn/blobs/a.webp', width: 480 }];
    expect(preconnectsOf('https://ddragon.leagueoflegends.com/cdn/img/a.jpg', stored)).toBeNull();
    expect(preconnectsOf('/cdn/blobs/a.jpg', [])).toBeNull();
  });
});
