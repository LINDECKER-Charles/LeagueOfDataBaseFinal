import { DOCUMENT, Injector } from '@angular/core';
import { WebPlatform } from './web-platform';

const ORIGIN = 'https://league-of-data-base.com';

interface FakeView {
  open: ReturnType<typeof vi.fn>;
  navigator: Partial<Pick<Navigator, 'share' | 'clipboard'>>;
}

function webPlatform(view: FakeView, anchor = document.createElement('a')): WebPlatform {
  const fakeDocument = {
    location: { origin: ORIGIN },
    defaultView: view,
    createElement: () => anchor,
  };
  return Injector.create({
    providers: [{ provide: DOCUMENT, useValue: fakeDocument }, WebPlatform],
  }).get(WebPlatform);
}

function view(navigator: FakeView['navigator'] = {}): FakeView {
  return { open: vi.fn(), navigator };
}

describe('WebPlatform', () => {
  afterEach(() => {
    vi.useRealTimers();
    vi.restoreAllMocks();
  });

  it('describes the browser target', () => {
    const platform = webPlatform(view());

    expect(platform.kind).toBe('web');
    expect(platform.authStrategy).toBe('cookie');
    expect(platform.updateState()).toBe('none');
    expect(platform.clientHeader()).toBeNull();
    expect(platform.apiOrigin()).toBe(ORIGIN);
  });

  it('opens http(s) links in a new context without opener', async () => {
    const window = view();

    await webPlatform(window).openExternal('https://www.leagueoflegends.com/');

    expect(window.open).toHaveBeenCalledWith(
      'https://www.leagueoflegends.com/',
      '_blank',
      'noopener,noreferrer',
    );
  });

  it.each(['javascript:alert(1)', 'data:text/html,x', 'file:///etc/passwd', 'champions'])(
    'refuses to open %s',
    async (url) => {
      const window = view();

      await expect(webPlatform(window).openExternal(url)).rejects.toThrow();
      expect(window.open).not.toHaveBeenCalled();
    },
  );

  it('shares through the native share sheet', async () => {
    const share = vi.fn(() => Promise.resolve());

    await expect(webPlatform(view({ share })).share({ url: ORIGIN })).resolves.toBe('shared');
    expect(share).toHaveBeenCalledWith({ url: ORIGIN });
  });

  it('reports a share sheet closed by the user as dismissed', async () => {
    const share = () => Promise.reject(new DOMException('Share canceled', 'AbortError'));

    await expect(webPlatform(view({ share })).share({ url: ORIGIN })).resolves.toBe('dismissed');
  });

  it('lets any other share failure surface', async () => {
    const share = () => Promise.reject(new DOMException('Not allowed', 'NotAllowedError'));

    await expect(webPlatform(view({ share })).share({ url: ORIGIN })).rejects.toThrow();
  });

  it('copies the link when the browser cannot share', async () => {
    const writeText = vi.fn(() => Promise.resolve());
    const clipboard = { writeText } as unknown as Clipboard;

    await expect(webPlatform(view({ clipboard })).share({ url: ORIGIN })).resolves.toBe('copied');
    expect(writeText).toHaveBeenCalledWith(ORIGIN);
  });

  it('fails when it can neither share nor copy', async () => {
    await expect(webPlatform(view()).share({ url: ORIGIN })).rejects.toThrow();
  });

  it('downloads a file through a short-lived object URL', async () => {
    vi.useFakeTimers();
    const objectUrl = `blob:${ORIGIN}/file`;
    const create = vi.spyOn(URL, 'createObjectURL').mockReturnValue(objectUrl);
    const revoke = vi.spyOn(URL, 'revokeObjectURL').mockReturnValue(undefined);
    const anchor = document.createElement('a');
    const click = vi.spyOn(anchor, 'click').mockReturnValue(undefined);
    const file = new File(['{}'], 'build.json', { type: 'application/json' });

    await webPlatform(view(), anchor).saveFile(file);

    expect(create).toHaveBeenCalledWith(file);
    expect(anchor.download).toBe('build.json');
    expect(click).toHaveBeenCalledOnce();
    expect(revoke).not.toHaveBeenCalled();
    vi.runAllTimers();
    expect(revoke).toHaveBeenCalledWith(objectUrl);
  });
});
