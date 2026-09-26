import { PLATFORM_ID, signal } from '@angular/core';
import { TestBed } from '@angular/core/testing';
import { of } from 'rxjs';
import type { PageContext } from '../../../../core/context/page-context';
import { CatalogueLists } from '../data/catalogue-lists';
import type { ListRequest } from '../data/list-request';
import { injectCatalogueNeighbours } from './inject-catalogue-neighbours';
import type { NeighbourQuery } from './neighbour-query';
import { neighboursOf } from './neighbours-of';

const CONTEXT: PageContext = { locale: 'en', version: '16.19.1', pinned: false, language: 'en_US' };
const CARDS = [{ id: 'Aatrox' }, { id: 'Ahri' }, { id: 'Akali' }];
const idOf = (card: { id: string }) => card.id;

describe('neighboursOf', () => {
  it('finds the entries on either side, without wrapping round', () => {
    expect(neighboursOf(CARDS, 'Ahri', idOf)).toEqual({ previous: CARDS[0], next: CARDS[2] });
    expect(neighboursOf(CARDS, 'Aatrox', idOf)).toEqual({ previous: null, next: CARDS[1] });
    expect(neighboursOf(CARDS, 'Akali', idOf)).toEqual({ previous: CARDS[1], next: null });
  });

  it('has none for an entry the list does not hold', () => {
    expect(neighboursOf(CARDS, 'Zed', idOf)).toEqual({ previous: null, next: null });
  });
});

describe('injectCatalogueNeighbours', () => {
  function neighboursIn(platform: 'browser' | 'server') {
    const requests: ListRequest[] = [];
    const fetch = (_: string, request: ListRequest) => {
      requests.push(request);
      return of({ kind: 'list', list: { entries: CARDS }, retryAfterMs: null });
    };
    TestBed.configureTestingModule({
      providers: [
        { provide: PLATFORM_ID, useValue: platform },
        { provide: CatalogueLists, useValue: { fetch } },
      ],
    });
    const at = signal<NeighbourQuery | undefined>({ context: CONTEXT, key: 'Ahri' });
    const neighbours = TestBed.runInInjectionContext(() =>
      injectCatalogueNeighbours('champions', at, (card) => card.id),
    );
    TestBed.tick();
    return { neighbours, requests, at };
  }

  it('reads the whole list once, and turns pages without another request', () => {
    const { neighbours, requests, at } = neighboursIn('browser');
    expect(neighbours().next?.id).toBe('Akali');
    at.set({ context: CONTEXT, key: 'Akali' });
    TestBed.tick();
    expect(neighbours()).toEqual({ previous: CARDS[1], next: null });
    expect(requests).toEqual([{ version: '16.19.1', lang: 'en_US' }]);
  });

  it('fetches nothing while rendering on the server', () => {
    const { neighbours, requests } = neighboursIn('server');
    expect(requests).toEqual([]);
    expect(neighbours()).toEqual({ previous: null, next: null });
  });
});
