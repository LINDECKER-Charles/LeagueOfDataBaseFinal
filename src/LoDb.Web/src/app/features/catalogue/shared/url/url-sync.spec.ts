import { PLATFORM_ID } from '@angular/core';
import { TestBed } from '@angular/core/testing';
import { Router, provideRouter } from '@angular/router';
import { facetOf } from '../testing/facet-of';
import type { FilterUrlSpec } from './filter-url-spec';
import type { FilterUrlState } from './filter-url-state';
import { FilterUrlSync } from './filter-url-sync';
import { TrailingThrottle } from './trailing-throttle';

const WINDOW_MS = 300;
const SPEC: FilterUrlSpec = {
  schema: [facetOf({ key: 'tag', kind: 'choice' })],
  defaultSize: 12,
};

describe('TrailingThrottle', () => {
  beforeEach(() => vi.useFakeTimers());
  afterEach(() => vi.useRealTimers());

  it('runs once at the end of the window, however often it was asked', () => {
    const action = vi.fn();
    const throttle = new TrailingThrottle(WINDOW_MS, action);
    throttle.request();
    vi.advanceTimersByTime(WINDOW_MS - 1);
    throttle.request();
    expect(action).not.toHaveBeenCalled();
    vi.advanceTimersByTime(1);
    expect(action).toHaveBeenCalledTimes(1);
    throttle.request();
    vi.advanceTimersByTime(WINDOW_MS);
    expect(action).toHaveBeenCalledTimes(2);
  });

  it('drops a pending run when cancelled', () => {
    const action = vi.fn();
    const throttle = new TrailingThrottle(WINDOW_MS, action);
    throttle.request();
    throttle.cancel();
    vi.advanceTimersByTime(WINDOW_MS);
    expect(action).not.toHaveBeenCalled();
  });
});

describe('FilterUrlSync', () => {
  beforeEach(() => vi.useFakeTimers());
  afterEach(() => vi.useRealTimers());

  async function syncAt(url: string, platform: 'browser' | 'server' = 'browser') {
    TestBed.configureTestingModule({
      providers: [
        provideRouter([{ path: '**', children: [] }]),
        { provide: PLATFORM_ID, useValue: platform },
        FilterUrlSync,
      ],
    });
    const router = TestBed.inject(Router);
    await router.navigateByUrl(url);
    const sync = TestBed.inject(FilterUrlSync);
    let state: FilterUrlState = { query: '', facets: {}, page: 1, size: 12 };
    const adopted: FilterUrlState[] = [];
    sync.bind({
      spec: () => SPEC,
      current: () => state,
      adopt: (next) => adopted.push(next),
    });
    const change = (next: Partial<FilterUrlState>) => {
      state = { ...state, ...next };
      sync.request();
    };
    return { router, sync, change, adopted };
  }

  it('reads the query of the current URL', async () => {
    const { sync } = await syncAt('/en/items?q=boots#top');
    expect(sync.search()).toBe('?q=boots');
  });

  it('writes the state back after the window, keeping the foreign parameters', async () => {
    const { router, change, adopted } = await syncAt('/en/items?lang=en_GB');
    change({ query: 'b' });
    change({ query: 'boots', facets: { tag: { values: ['Boots'], all: false } } });
    expect(router.url).toBe('/en/items?lang=en_GB');
    await vi.advanceTimersByTimeAsync(WINDOW_MS);
    expect(router.url).toBe('/en/items?lang=en_GB&q=boots&tag=Boots');
    expect(adopted).toEqual([]);
  });

  it('replaces the history entry rather than adding one', async () => {
    const { router, change } = await syncAt('/en/items');
    const navigate = vi.spyOn(router, 'navigateByUrl');
    change({ page: 2 });
    await vi.advanceTimersByTimeAsync(WINDOW_MS);
    expect(navigate).toHaveBeenCalledWith('/en/items?page=2', { replaceUrl: true });
  });

  it('does not navigate when the URL already says it', async () => {
    const { router, change } = await syncAt('/en/items?page=2');
    const navigate = vi.spyOn(router, 'navigateByUrl');
    change({ page: 2 });
    await vi.advanceTimersByTimeAsync(WINDOW_MS);
    expect(navigate).not.toHaveBeenCalled();
  });

  it('never writes while rendering on the server', async () => {
    const { router, change } = await syncAt('/en/items', 'server');
    change({ query: 'boots' });
    await vi.advanceTimersByTimeAsync(WINDOW_MS);
    expect(router.url).toBe('/en/items');
  });

  it('hands back the state of a navigation it did not ask for', async () => {
    const { router, adopted } = await syncAt('/en/items?q=boots');
    await router.navigateByUrl('/en/items?tag=Vision&page=3');
    expect(adopted).toEqual([
      { query: '', facets: { tag: { values: ['Vision'], all: false } }, page: 3, size: 12 },
    ]);
  });

  it('builds the link of another state from the current URL', async () => {
    const { sync } = await syncAt('/en/items?lang=en_GB&page=2');
    expect(sync.urlOf({ query: '', facets: {}, page: 3, size: 12 })).toBe(
      '/en/items?lang=en_GB&page=3',
    );
  });
});
