import { FakeCache } from '../testing/fake-cache';
import { FakeFetchEvent } from '../testing/fake-fetch-event';
import { sameOriginResponse } from '../testing/same-origin-response';
import { answerFromCache } from './answer-from-cache';

const BLOB_URL = 'https://league-of-data-base.com/cdn/blobs/ab/cd/abcdef.webp';

describe('answerFromCache', () => {
  let cache: FakeCache;
  let event: FakeFetchEvent;
  let fetchFile: ReturnType<typeof vi.fn<(request: Request) => Promise<Response>>>;

  beforeEach(() => {
    cache = new FakeCache();
    event = new FakeFetchEvent(new Request(BLOB_URL));
    fetchFile = vi.fn<(request: Request) => Promise<Response>>();
    vi.stubGlobal('fetch', fetchFile);
  });

  afterEach(() => {
    vi.unstubAllGlobals();
  });

  it('answers a kept file without going to the network', async () => {
    await cache.put(BLOB_URL, new Response('kept'));

    const response = await answerFromCache(event, { cache: cache.asCache(), limit: 10 });

    expect(await response.text()).toBe('kept');
    expect(fetchFile).not.toHaveBeenCalled();
  });

  it('fetches a new file and keeps it', async () => {
    fetchFile.mockResolvedValue(sameOriginResponse('fetched'));

    const response = await answerFromCache(event, { cache: cache.asCache(), limit: 10 });
    await event.settled();

    expect(await response.text()).toBe('fetched');
    expect(cache.entries.has(BLOB_URL)).toBe(true);
  });

  it('keeps no more files than its limit, dropping the oldest', async () => {
    await cache.put('https://league-of-data-base.com/cdn/blobs/old.webp', new Response('old'));
    fetchFile.mockResolvedValue(sameOriginResponse('fetched'));

    await answerFromCache(event, { cache: cache.asCache(), limit: 1 });
    await event.settled();

    expect([...cache.entries.keys()]).toEqual([BLOB_URL]);
  });

  it('fails like the network when offline and not kept', async () => {
    fetchFile.mockRejectedValue(new TypeError('Failed to fetch'));

    const response = await answerFromCache(event, { cache: cache.asCache(), limit: 10 });

    expect(response.type).toBe('error');
  });

  // An image opened in its own tab is a navigation: the browser preloaded it already.
  it('answers a file opened as a page with its navigation preload, fetched once', async () => {
    const preloaded = Promise.resolve(sameOriginResponse('preloaded'));
    event = new FakeFetchEvent(new Request(BLOB_URL), preloaded);

    const response = await answerFromCache(event, { cache: cache.asCache(), limit: 10 });
    await event.settled();

    expect(await response.text()).toBe('preloaded');
    expect(fetchFile).not.toHaveBeenCalled();
    expect(cache.entries.has(BLOB_URL)).toBe(true);
  });
});
