import type { CacheSlot } from '../storage/cache-slot';
import { FakeCache } from '../testing/fake-cache';
import { FakeFetchEvent } from '../testing/fake-fetch-event';
import { sameOriginResponse } from '../testing/same-origin-response';
import { answerPage } from './answer-page';

const PAGE_URL = 'https://league-of-data-base.com/en/champions';
const OFFLINE_URL = 'https://league-of-data-base.com/offline.html';

describe('answerPage', () => {
  let pages: FakeCache;
  let offline: FakeCache;
  let event: FakeFetchEvent;
  let fetchPage: ReturnType<typeof vi.fn<(request: Request) => Promise<Response>>>;

  function slot(): CacheSlot {
    return { cache: pages.asCache(), limit: 40 };
  }

  function answer(): Promise<Response> {
    return answerPage(event, slot(), offline.asCache());
  }

  beforeEach(() => {
    pages = new FakeCache();
    offline = new FakeCache();
    event = new FakeFetchEvent(new Request(PAGE_URL));
    fetchPage = vi.fn<(request: Request) => Promise<Response>>();
    vi.stubGlobal('fetch', fetchPage);
  });

  afterEach(() => {
    vi.unstubAllGlobals();
    vi.useRealTimers();
  });

  it('answers the fresh page and keeps a copy of it', async () => {
    fetchPage.mockResolvedValue(sameOriginResponse('fresh'));

    const response = await answer();
    await event.settled();

    expect(await response.text()).toBe('fresh');
    expect(await (await pages.match(PAGE_URL))?.text()).toBe('fresh');
  });

  it('keeps no copy of a private page or of an error', async () => {
    fetchPage.mockResolvedValueOnce(
      sameOriginResponse('mine', { headers: { 'Cache-Control': 'private, no-store' } }),
    );
    await answer();
    await event.settled();
    fetchPage.mockResolvedValueOnce(sameOriginResponse('gone', { status: 404 }));
    await answer();
    await event.settled();

    expect(pages.entries.size).toBe(0);
  });

  it('answers the last copy when the network fails', async () => {
    await pages.put(PAGE_URL, new Response('copy'));
    fetchPage.mockRejectedValue(new TypeError('Failed to fetch'));

    expect(await (await answer()).text()).toBe('copy');
  });

  it('answers the offline page for an uncached page when the network fails', async () => {
    await offline.put(OFFLINE_URL, new Response('offline'));
    fetchPage.mockRejectedValue(new TypeError('Failed to fetch'));

    expect(await (await answer()).text()).toBe('offline');
  });

  it('still answers a page when even the offline page is missing', async () => {
    fetchPage.mockRejectedValue(new TypeError('Failed to fetch'));

    const response = await answer();

    expect(response.status).toBe(503);
    expect(response.headers.get('Content-Type')).toContain('text/html');
    expect(await response.text()).toContain('offline');
  });

  it('answers the copy of a page after 4 s of a slow network, then refreshes it', async () => {
    vi.useFakeTimers();
    await pages.put(PAGE_URL, new Response('copy'));
    let arrive: (response: Response) => void = () => undefined;
    fetchPage.mockReturnValue(new Promise((resolve) => (arrive = resolve)));

    const response = answer();
    await vi.advanceTimersByTimeAsync(4000);

    expect(await (await response).text()).toBe('copy');
    arrive(sameOriginResponse('late'));
    await event.settled();
    expect(await (await pages.match(PAGE_URL))?.text()).toBe('late');
  });

  it('waits for a slow network when no copy could answer instead', async () => {
    vi.useFakeTimers();
    let arrive: (response: Response) => void = () => undefined;
    fetchPage.mockReturnValue(new Promise((resolve) => (arrive = resolve)));
    let answered = false;

    const response = answer().then((value) => {
      answered = true;
      return value;
    });
    await vi.advanceTimersByTimeAsync(30_000);
    expect(answered).toBe(false);
    arrive(sameOriginResponse('slow'));

    expect(await (await response).text()).toBe('slow');
  });

  it('answers a navigation with the page preloaded while the worker started, once', async () => {
    const preloaded = Promise.resolve(sameOriginResponse('preloaded'));
    event = new FakeFetchEvent(new Request(PAGE_URL), preloaded);

    const response = await answer();
    await event.settled();

    expect(await response.text()).toBe('preloaded');
    expect(fetchPage).not.toHaveBeenCalled();
    expect(await (await pages.match(PAGE_URL))?.text()).toBe('preloaded');
  });

  it('fetches the page itself when the browser preloaded nothing', async () => {
    event = new FakeFetchEvent(new Request(PAGE_URL), Promise.resolve(undefined));
    fetchPage.mockResolvedValue(sameOriginResponse('fetched'));

    expect(await (await answer()).text()).toBe('fetched');
    expect(fetchPage).toHaveBeenCalledOnce();
  });

  it('answers the last copy when the preload fails', async () => {
    await pages.put(PAGE_URL, new Response('copy'));
    event = new FakeFetchEvent(new Request(PAGE_URL), Promise.reject(new TypeError('offline')));

    expect(await (await answer()).text()).toBe('copy');
    expect(fetchPage).not.toHaveBeenCalled();
  });

  it('works without storage', async () => {
    fetchPage.mockResolvedValue(sameOriginResponse('fresh'));

    const response = await answerPage(event, undefined, undefined);

    expect(await response.text()).toBe('fresh');
  });
});
