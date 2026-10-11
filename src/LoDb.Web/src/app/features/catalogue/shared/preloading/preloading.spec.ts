import { PLATFORM_ID } from '@angular/core';
import { TestBed } from '@angular/core/testing';
import type { Route } from '@angular/router';
import { of } from 'rxjs';
import { CataloguePreloading } from './catalogue-preloading';
import { PRELOADED_ROUTE } from './preloaded-route';

// The pause after the load event where the browser has no requestIdleCallback (jsdom).
const IDLE_FALLBACK_MS = 200;

describe('CataloguePreloading', () => {
  afterEach(() => {
    vi.useRealTimers();
    Reflect.deleteProperty(navigator, 'connection');
  });

  function strategyOn(platform: 'browser' | 'server'): CataloguePreloading {
    TestBed.configureTestingModule({ providers: [{ provide: PLATFORM_ID, useValue: platform }] });
    return TestBed.inject(CataloguePreloading);
  }

  function preloads(strategy: CataloguePreloading, route: Route): () => boolean {
    let loaded = false;
    strategy
      .preload(route, () => {
        loaded = true;
        return of(null);
      })
      .subscribe();
    return () => loaded;
  }

  it('reads the catalogue ahead once the page is idle, and nothing before', () => {
    vi.useFakeTimers();
    expect(document.readyState).toBe('complete');
    const loaded = preloads(strategyOn('browser'), { path: 'champions', data: PRELOADED_ROUTE });

    expect(loaded()).toBe(false);
    vi.advanceTimersByTime(IDLE_FALLBACK_MS);
    expect(loaded()).toBe(true);
  });

  it('never reads another section ahead, nor on the server', () => {
    vi.useFakeTimers();
    const account = preloads(strategyOn('browser'), { path: 'account' });
    TestBed.resetTestingModule();
    const onServer = preloads(strategyOn('server'), { path: 'items', data: PRELOADED_ROUTE });

    vi.advanceTimersByTime(60_000);
    expect(account()).toBe(false);
    expect(onServer()).toBe(false);
  });

  it('reads nothing ahead under Save-Data', () => {
    vi.useFakeTimers();
    Object.defineProperty(navigator, 'connection', {
      configurable: true,
      value: { saveData: true },
    });
    const loaded = preloads(strategyOn('browser'), { path: 'runes', data: PRELOADED_ROUTE });

    vi.advanceTimersByTime(60_000);
    expect(loaded()).toBe(false);
  });
});
