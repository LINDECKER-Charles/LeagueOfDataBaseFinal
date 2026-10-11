import type { ExtendableEventLike } from './extendable-event-like';

/** The part of a service worker's FetchEvent this worker uses. */
export interface FetchEventLike extends ExtendableEventLike {
  readonly request: Request;
  /**
   * The navigation preload the browser sent while the worker was starting: undefined when
   * it sent none (not a navigation, preload off), absent where the browser has no preload.
   */
  readonly preloadResponse?: Promise<Response | undefined>;
  respondWith(response: Promise<Response>): void;
}
