import { RESPONSE_INIT } from '@angular/core';
import { TestBed } from '@angular/core/testing';
import type { ImageVariant } from '../../../../core/api/generated/models/image-variant';
import { injectArtPreconnect } from './inject-art-preconnect';

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
