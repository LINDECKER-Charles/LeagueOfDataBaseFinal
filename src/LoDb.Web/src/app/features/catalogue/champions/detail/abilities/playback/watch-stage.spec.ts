import { watchStage } from './watch-stage';

type Listener = (entries: Partial<IntersectionObserverEntry>[]) => void;

describe('watchStage', () => {
  afterEach(() => vi.unstubAllGlobals());

  it('reports the stage on screen from half of it, at once and on each crossing', () => {
    let notify: Listener = () => undefined;
    let options: IntersectionObserverInit | undefined;
    const disconnect = vi.fn();
    vi.stubGlobal(
      'IntersectionObserver',
      class {
        constructor(listener: Listener, init: IntersectionObserverInit) {
          notify = listener;
          options = init;
        }
        observe = vi.fn();
        disconnect = disconnect;
      },
    );
    const seen: boolean[] = [];

    const stop = watchStage(document.createElement('div'), (isOnScreen) => seen.push(isOnScreen));
    notify([{ isIntersecting: true, intersectionRatio: 0.2 }]);
    notify([{ isIntersecting: true, intersectionRatio: 0.6 }]);
    notify([{ isIntersecting: false, intersectionRatio: 0 }]);
    stop();

    expect(options?.threshold).toBe(0.5);
    expect(seen).toEqual([false, true, false]);
    expect(disconnect).toHaveBeenCalled();
  });

  it('counts the stage on screen where the browser cannot tell', () => {
    vi.stubGlobal('IntersectionObserver', undefined);
    const seen: boolean[] = [];

    watchStage(document.createElement('div'), (isOnScreen) => seen.push(isOnScreen));

    expect(seen).toEqual([true]);
  });
});
