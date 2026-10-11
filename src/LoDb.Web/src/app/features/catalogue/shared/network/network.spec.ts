import { RESPONSE_INIT } from '@angular/core';
import { TestBed } from '@angular/core/testing';
import type { ImageVariant } from '../../../../core/api/generated/models/image-variant';
import { connectionOf } from './connection-of';
import { injectArtPreconnect } from './inject-art-preconnect';
import { wantsLightData } from './wants-light-data';
import { whenIdleAfterLoad } from './when-idle-after-load';

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

describe('whenIdleAfterLoad', () => {
  afterEach(() => vi.useRealTimers());

  function viewAt(readyState: DocumentReadyState, idle?: (task: () => void) => number) {
    const target = new EventTarget();
    const view = {
      document: { readyState },
      addEventListener: target.addEventListener.bind(target),
      removeEventListener: target.removeEventListener.bind(target),
      ...(idle === undefined ? {} : { requestIdleCallback: idle, cancelIdleCallback: vi.fn() }),
    };
    return { view: view as unknown as Window, target };
  }

  it('waits for the load event, then for an idle moment', () => {
    const idle = vi.fn((task: () => void) => {
      task();
      return 1;
    });
    const { view, target } = viewAt('interactive', idle);
    const task = vi.fn();

    whenIdleAfterLoad(view, task);
    expect(task).not.toHaveBeenCalled();
    target.dispatchEvent(new Event('load'));

    expect(idle).toHaveBeenCalledOnce();
    expect(task).toHaveBeenCalledOnce();
  });

  it('only waits a short pause where the browser has no idle callback', () => {
    vi.useFakeTimers();
    const { view } = viewAt('complete');
    const task = vi.fn();

    whenIdleAfterLoad(view, task);
    expect(task).not.toHaveBeenCalled();
    vi.advanceTimersByTime(200);

    expect(task).toHaveBeenCalledOnce();
  });

  it('runs nothing once cancelled', () => {
    vi.useFakeTimers();
    const { view, target } = viewAt('loading');
    const task = vi.fn();

    whenIdleAfterLoad(view, task)();
    target.dispatchEvent(new Event('load'));
    vi.advanceTimersByTime(5000);

    expect(task).not.toHaveBeenCalled();
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
