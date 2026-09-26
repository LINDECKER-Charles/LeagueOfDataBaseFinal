/**
 * The part of a service worker's ExtendableEvent this worker uses. The DOM library the app is
 * type-checked with has no worker event types, and the WebWorker library would clash with it.
 */
export interface ExtendableEventLike {
  waitUntil(work: Promise<unknown>): void;
}
