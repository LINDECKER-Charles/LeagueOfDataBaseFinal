import { PLATFORM_ID, RESPONSE_INIT, signal } from '@angular/core';
import { TestBed } from '@angular/core/testing';
import { Router, provideRouter } from '@angular/router';
import { type Observable, of } from 'rxjs';
import type { PageContext } from '../../../../core/context/page-context';
import { CatalogueLists } from '../data/catalogue-lists';
import type { ListOutcome } from '../data/list-outcome';
import type { ListRequest } from '../data/list-request';
import { injectCatalogueList } from './inject-catalogue-list';

const CONTEXT: PageContext = { locale: 'en', version: '16.19.1', pinned: false, language: 'en_US' };
const TRANSIENT = 'public, max-age=0, s-maxage=60';

type Answer = (request: ListRequest) => ListOutcome<string>;

const ready = (request: ListRequest): ListOutcome<string> => ({
  kind: 'list',
  list: request.page === undefined ? 'whole' : `page ${request.page}`,
  retryAfterMs: null,
});

async function sourceAt(url: string, platform: 'browser' | 'server', answer: Answer = ready) {
  const requests: ListRequest[] = [];
  const init: ResponseInit = { headers: new Headers() };
  const fetch = (_: string, request: ListRequest): Observable<ListOutcome<string>> => {
    requests.push(request);
    return of(answer(request));
  };
  TestBed.configureTestingModule({
    providers: [
      provideRouter([{ path: '**', children: [] }]),
      { provide: PLATFORM_ID, useValue: platform },
      { provide: RESPONSE_INIT, useValue: init },
      { provide: CatalogueLists, useValue: { fetch } },
    ],
  });
  await TestBed.inject(Router).navigateByUrl(url);
  const context = signal<PageContext | undefined>(CONTEXT);
  const source = TestBed.runInInjectionContext(() => injectCatalogueList('items', context));
  TestBed.tick();
  return { source, requests, init, context };
}

describe('injectCatalogueList', () => {
  afterEach(() => vi.useRealTimers());

  it('renders on the server the page the URL names, and only that page', async () => {
    const { source, requests, init } = await sourceAt('/en/items?page=2&size=24', 'server');
    expect(requests).toEqual([{ version: '16.19.1', lang: 'en_US', page: 2, size: 24 }]);
    expect(source.slice).toEqual({ page: 2, size: 24 });
    expect(source.firstPage()).toBe('page 2');
    expect(source.dataset()).toBeNull();
    expect(source.status()).toBe('ready');
    expect(new Headers(init.headers).get('Cache-Control')).toBeNull();
  });

  it('lets the proxy keep a server render with placeholders a minute only', async () => {
    const cold: Answer = (request) => ({ ...ready(request), retryAfterMs: 5000 });
    const { init } = await sourceAt('/en/items', 'server', cold);
    expect(new Headers(init.headers).get('Cache-Control')).toBe(TRANSIENT);
  });

  it('fetches in the browser the first page, then the whole list for filtering', async () => {
    const { source, requests } = await sourceAt('/en/items?q=boots', 'browser');
    expect(requests).toEqual([
      { version: '16.19.1', lang: 'en_US', page: 1, size: 12 },
      { version: '16.19.1', lang: 'en_US' },
    ]);
    expect(source.firstPage()).toBe('page 1');
    expect(source.dataset()).toBe('whole');
    expect(source.list()).toBe('whole');
  });

  it('asks once when the URL already names the whole list', async () => {
    const { source, requests } = await sourceAt('/en/items?size=all', 'browser');
    expect(requests).toEqual([{ version: '16.19.1', lang: 'en_US' }]);
    expect(source.dataset()).toBe('whole');
  });

  it('retries the whole list once after its Retry-After, never more', async () => {
    vi.useFakeTimers();
    const cold: Answer = (request) =>
      request.page === undefined ? { kind: 'pending', retryAfterMs: 5000 } : ready(request);
    const { source, requests } = await sourceAt('/en/items', 'browser', cold);
    expect(source.status()).toBe('pending');
    await vi.advanceTimersByTimeAsync(60_000);
    TestBed.tick();
    expect(requests.filter((request) => request.page === undefined)).toHaveLength(2);
    expect(source.list()).toBe('page 1');
  });

  it('refetches when the language changes, not on a mere query change', async () => {
    const { requests, context } = await sourceAt('/en/items', 'browser');
    await TestBed.inject(Router).navigateByUrl('/en/items?q=ward');
    TestBed.tick();
    expect(requests).toHaveLength(2);
    context.set({ ...CONTEXT, language: 'en_GB' });
    TestBed.tick();
    expect(requests.map((request) => request.lang)).toEqual(['en_US', 'en_US', 'en_GB', 'en_GB']);
  });
});
