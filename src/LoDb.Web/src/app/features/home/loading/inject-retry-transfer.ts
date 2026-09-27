import { isPlatformServer } from '@angular/common';
import { PLATFORM_ID, TransferState, inject, makeStateKey } from '@angular/core';

const RETRY_AFTER = makeStateKey<number | null>('home.retryAfterMs');

/**
 * Carries the previews' `Retry-After` from the server to the hydrating browser. The HTTP
 * transfer cache hands the browser the server's answers without their headers, so the first
 * resolution in the browser would read no delay and never read the pending images again.
 * The server writes the delay it read; the browser takes it once, when its own answers carry
 * none, and every later resolution reads its own headers.
 */
export function injectRetryTransfer(): (retryAfterMs: number | null) => number | null {
  const state = inject(TransferState);
  const isServer = isPlatformServer(inject(PLATFORM_ID));
  return (retryAfterMs) => {
    if (isServer) {
      state.set(RETRY_AFTER, retryAfterMs);
      return retryAfterMs;
    }
    const carried = state.get(RETRY_AFTER, null);
    state.remove(RETRY_AFTER);
    return retryAfterMs ?? carried;
  };
}
