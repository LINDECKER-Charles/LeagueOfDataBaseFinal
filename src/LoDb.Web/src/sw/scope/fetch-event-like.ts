import type { ExtendableEventLike } from './extendable-event-like';

/** The part of a service worker's FetchEvent this worker uses. */
export interface FetchEventLike extends ExtendableEventLike {
  readonly request: Request;
  respondWith(response: Promise<Response>): void;
}
