/**
 * A Cache of the Cache Storage API, in memory, for specs: entries keyed by URL, in insertion
 * order like the browser's. Neither jsdom nor Node has the API.
 */
export class FakeCache {
  readonly entries = new Map<string, Response>();

  asCache(): Cache {
    return this as unknown as Cache;
  }

  match(request: RequestInfo | URL): Promise<Response | undefined> {
    return Promise.resolve(this.entries.get(urlOf(request))?.clone());
  }

  put(request: RequestInfo | URL, response: Response): Promise<void> {
    this.entries.set(urlOf(request), response);
    return Promise.resolve();
  }

  keys(): Promise<Request[]> {
    return Promise.resolve([...this.entries.keys()].map((url) => new Request(url)));
  }

  delete(request: RequestInfo | URL): Promise<boolean> {
    return Promise.resolve(this.entries.delete(urlOf(request)));
  }
}

function urlOf(request: RequestInfo | URL): string {
  if (typeof request === 'string') {
    return new URL(request, 'https://league-of-data-base.com').href;
  }
  return request instanceof URL ? request.href : request.url;
}
