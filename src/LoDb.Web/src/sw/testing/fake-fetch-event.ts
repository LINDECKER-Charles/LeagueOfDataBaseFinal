import type { FetchEventLike } from '../scope/fetch-event-like';

/** A FetchEvent for specs, which keeps what the worker hands to waitUntil. */
export class FakeFetchEvent implements FetchEventLike {
  readonly extensions: Promise<unknown>[] = [];

  constructor(readonly request: Request) {}

  waitUntil(work: Promise<unknown>): void {
    this.extensions.push(work);
  }

  respondWith(): void {
    throw new Error('The strategies return their response; the worker calls respondWith.');
  }

  /** Settles once everything handed to waitUntil has. */
  settled(): Promise<unknown> {
    return Promise.allSettled(this.extensions);
  }
}
