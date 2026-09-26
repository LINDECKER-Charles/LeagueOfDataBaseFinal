import type { ExtendableEventLike } from './extendable-event-like';
import type { FetchEventLike } from './fetch-event-like';

/** The part of ServiceWorkerGlobalScope this worker uses. */
export interface WorkerScope {
  readonly location: { readonly origin: string };
  readonly registration: {
    readonly navigationPreload?: { disable(): Promise<void> };
  };
  readonly clients: { claim(): Promise<void> };
  skipWaiting(): Promise<void>;
  addEventListener(
    type: 'install' | 'activate',
    listener: (event: ExtendableEventLike) => void,
  ): void;
  addEventListener(type: 'fetch', listener: (event: FetchEventLike) => void): void;
}
