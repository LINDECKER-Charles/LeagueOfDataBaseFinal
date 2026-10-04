import { HttpStatusCode } from '@angular/common/http';
import { Injectable, RESPONSE_INIT, inject } from '@angular/core';
import type { PageOutcome } from '../outcome/page-outcome';
import type { CacheClass } from './cache-class';
import { CACHE_CONTROL } from './cache-control';
import { NOINDEX_HEADERS } from './noindex-headers';

/**
 * Status and headers of the server response of the page being rendered, written into
 * `RESPONSE_INIT`: the only way to answer a real 301, 302, 404 or 503 without letting the
 * router redirect, which the SSR engine would turn into a 302. In the browser, and while
 * prerendering, there is no response to write and every call does nothing.
 */
@Injectable({ providedIn: 'root' })
export class PageResponse {
  private readonly init = inject(RESPONSE_INIT, { optional: true });
  private isStorable = true;

  /** Sets the `Cache-Control` of a class of response, unless storage was forbidden. */
  cache(kind: CacheClass): void {
    this.header('Cache-Control', CACHE_CONTROL[this.isStorable ? kind : 'private']);
  }

  /**
   * Keeps this response out of every cache, whatever a resolver sets afterwards: a render
   * missing part of itself, such as a catalogue that failed to load, must not be served to
   * every visitor and crawler for minutes, or a week for a pinned version.
   */
  forbidStorage(): void {
    this.isStorable = false;
    this.cache('private');
  }

  /** Answers an outcome instead of the page: its status, `Location`, cache and robots. */
  answer(outcome: PageOutcome): void {
    this.cache('transient');
    if (outcome.kind === 'redirect') {
      this.status(outcome.status);
      this.header('Location', outcome.location);
      return;
    }
    const failure = outcome.kind === 'failure';
    this.status(failure ? outcome.status : HttpStatusCode.NotFound);
    this.header('Retry-After', failure ? outcome.retryAfter : null);
    for (const [name, value] of Object.entries(NOINDEX_HEADERS)) {
      this.header(name, value);
    }
  }

  private status(status: HttpStatusCode): void {
    if (this.init !== null) {
      this.init.status = status;
    }
  }

  // Null removes the header. The engine passes `Headers`; any other form is converted once.
  private header(name: string, value: string | null): void {
    const init = this.init;
    if (init === null) {
      return;
    }
    const headers = init.headers instanceof Headers ? init.headers : new Headers(init.headers);
    init.headers = headers;
    if (value === null) {
      headers.delete(name);
    } else {
      headers.set(name, value);
    }
  }
}
