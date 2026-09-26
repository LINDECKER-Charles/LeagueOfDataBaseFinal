import { registerWorker } from './register-worker';

interface FakeView {
  view: Window;
  register: ReturnType<typeof vi.fn>;
  fireLoad: () => void;
}

function fakeView(readyState: DocumentReadyState, withWorkers = true): FakeView {
  const register = vi.fn().mockResolvedValue({});
  let onLoad: () => void = () => undefined;
  const document = new DOMParser().parseFromString('<!doctype html><head></head>', 'text/html');
  Object.defineProperty(document, 'readyState', { value: readyState });
  const view = {
    document,
    navigator: withWorkers ? { serviceWorker: { register } } : {},
    addEventListener: (_type: string, listener: () => void) => (onLoad = listener),
  } as unknown as Window;
  return { view, register, fireLoad: () => onLoad() };
}

describe('registerWorker', () => {
  it('registers /sw.js for the whole site once the page has loaded', () => {
    const { view, register, fireLoad } = fakeView('interactive');

    registerWorker(view);
    expect(register).not.toHaveBeenCalled();
    fireLoad();

    expect(register).toHaveBeenCalledWith('/sw.js', { scope: '/', updateViaCache: 'none' });
  });

  it('registers at once when the page has already loaded', () => {
    const { view, register } = fakeView('complete');

    registerWorker(view);

    expect(register).toHaveBeenCalledTimes(1);
  });

  it('makes the site installable, once', () => {
    const { view } = fakeView('complete');

    registerWorker(view);
    registerWorker(view);

    const head = view.document.head;
    expect(head.querySelectorAll('link[rel="manifest"]')).toHaveLength(1);
    expect(head.querySelector('link[rel="manifest"]')?.getAttribute('href')).toBe(
      '/manifest.webmanifest',
    );
    expect(head.querySelectorAll('link[rel="apple-touch-icon"]')).toHaveLength(1);
    expect(head.querySelector('meta[name="theme-color"]')?.getAttribute('content')).toBe('#010a13');
  });

  it('leaves a browser without service workers as it is', () => {
    const { view } = fakeView('complete', false);

    expect(() => registerWorker(view)).not.toThrow();
  });

  it('ignores a refused registration', async () => {
    const { view, register } = fakeView('complete');
    register.mockRejectedValue(new DOMException('Denied', 'SecurityError'));

    registerWorker(view);
    await Promise.resolve();

    expect(register).toHaveBeenCalledTimes(1);
  });
});
