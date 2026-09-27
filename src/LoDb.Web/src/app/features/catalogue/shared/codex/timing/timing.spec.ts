import { PLATFORM_ID, RESPONSE_INIT } from '@angular/core';
import { TestBed } from '@angular/core/testing';
import {
  type ActivatedRouteSnapshot,
  type ResolveFn,
  type RouterStateSnapshot,
  provideRouter,
} from '@angular/router';
import { provideTransloco } from '@jsverse/transloco';
import { of } from 'rxjs';
import { LoadClock } from './load-clock';
import { LoadTime } from './load-time';
import { loadTimesOf } from './load-times-of';
import { withLoadTiming } from './with-load-timing';

function performanceOf(entries: unknown[], now = 900): Performance {
  return { getEntriesByType: () => entries, now: () => now } as unknown as Performance;
}

describe('loadTimesOf', () => {
  const first = { initial: true, startedAt: 0, fetchMs: 12 };

  it('reads the first page of a visit from its Navigation Timing entry', () => {
    const entry = {
      startTime: 0,
      responseEnd: 120,
      domContentLoadedEventEnd: 310,
      serverTiming: [
        { name: 'db', duration: 3 },
        { name: 'catalogue', duration: 42.5 },
      ],
    };
    expect(loadTimesOf(first, performanceOf([entry]))).toEqual({ serverMs: 42.5, clientMs: 310 });
  });

  it('ends at the response while the document is still parsing, without a server share', () => {
    const entry = { startTime: 5, responseEnd: 125, domContentLoadedEventEnd: 0, serverTiming: [] };
    expect(loadTimesOf(first, performanceOf([entry]))).toEqual({ serverMs: null, clientMs: 120 });
  });

  it('counts from the time origin without a Navigation Timing entry', () => {
    expect(loadTimesOf(first, performanceOf([], 640))).toEqual({ serverMs: null, clientMs: 640 });
  });

  it('counts the fetch, then the render, of a page reached in the browser', () => {
    const later = { initial: false, startedAt: 500, fetchMs: 80 };
    expect(loadTimesOf(later, performanceOf([], 900))).toEqual({ serverMs: 80, clientMs: 400 });
  });
});

describe('withLoadTiming', () => {
  const route = {} as ActivatedRouteSnapshot;
  const state = {} as RouterStateSnapshot;

  function run<T>(inner: ResolveFn<T>): Promise<unknown> {
    const timed = withLoadTiming(inner);
    return TestBed.runInInjectionContext(() => timed(route, state)) as Promise<unknown>;
  }

  beforeEach(() => {
    vi.spyOn(performance, 'now').mockReturnValueOnce(100).mockReturnValueOnce(142.34);
  });

  afterEach(() => {
    vi.restoreAllMocks();
  });

  it('sends the time of the entity as Server-Timing on the server', async () => {
    const init: ResponseInit = { status: 200, headers: new Headers({ 'Cache-Control': 'public' }) };
    TestBed.configureTestingModule({
      providers: [
        provideRouter([]),
        { provide: PLATFORM_ID, useValue: 'server' },
        { provide: RESPONSE_INIT, useValue: init },
      ],
    });

    await expect(run(() => Promise.resolve('Annie'))).resolves.toBe('Annie');

    const headers = init.headers as Headers;
    expect(headers.get('Server-Timing')).toBe('catalogue;dur=42.3');
    expect(headers.get('Cache-Control')).toBe('public');
    expect(TestBed.inject(LoadClock).last()).toBeNull();
  });

  it('notes the fetch of the first page in the browser, an observable one included', async () => {
    TestBed.configureTestingModule({ providers: [provideRouter([])] });

    await expect(run(() => of('Annie'))).resolves.toBe('Annie');

    expect(TestBed.inject(LoadClock).last()).toEqual({
      initial: true,
      startedAt: 100,
      fetchMs: expect.closeTo(42.34, 5),
    });
  });
});

describe('lodb-load-time', () => {
  beforeEach(() => {
    TestBed.configureTestingModule({
      providers: [
        provideTransloco({
          config: { defaultLang: 'en', missingHandler: { logMissingKey: false }, prodMode: true },
          loader: class {
            getTranslation = () => of({});
          },
        }),
      ],
    });
  });

  afterEach(() => {
    vi.restoreAllMocks();
  });

  it('shows nothing before a page is measured', async () => {
    const fixture = TestBed.createComponent(LoadTime);
    await fixture.whenStable();

    expect((fixture.nativeElement as HTMLElement).querySelector('.perf')).toBeNull();
  });

  it('shows the server and client shares of the page just rendered', async () => {
    vi.spyOn(performance, 'now').mockReturnValue(1000);
    TestBed.inject(LoadClock).last.set({ initial: false, startedAt: 750.4, fetchMs: 61.6 });
    const fixture = TestBed.createComponent(LoadTime);
    await fixture.whenStable();

    const values = (fixture.nativeElement as HTMLElement).querySelectorAll('.perf__value');
    expect([...values].map((value) => value.textContent)).toEqual(['62 ms', '250 ms']);
  });

  it('marks an unknown server share with a dash', async () => {
    TestBed.inject(LoadClock).last.set({ initial: true, startedAt: 0, fetchMs: 0 });
    vi.spyOn(performance, 'getEntriesByType').mockReturnValue([]);
    const fixture = TestBed.createComponent(LoadTime);
    await fixture.whenStable();

    const value = (fixture.nativeElement as HTMLElement).querySelector('.perf__value');
    expect(value?.textContent).toBe('—');
  });
});
