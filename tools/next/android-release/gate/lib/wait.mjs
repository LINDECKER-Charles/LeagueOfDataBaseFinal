// Waiting on an emulator: its answers come late, and fail while the app restarts.

const POLL_INTERVAL_MS = 1_000;

export function sleep(ms) {
  return new Promise((resolve) => setTimeout(resolve, ms));
}

/**
 * Asks `check` until it answers true; its failures count as "not yet". Throws after
 * `timeoutMs`, with the last failure, naming what it waited for.
 */
export async function poll(check, { timeoutMs, what }) {
  const deadline = Date.now() + timeoutMs;
  let lastFailure = null;
  while (Date.now() < deadline) {
    try {
      if (await check()) {
        return;
      }
      lastFailure = null;
    } catch (error) {
      lastFailure = error;
    }
    await sleep(POLL_INTERVAL_MS);
  }
  const cause = lastFailure === null ? '' : ` (last failure: ${lastFailure.message})`;
  throw new Error(`waited ${timeoutMs} ms for ${what}${cause}`);
}
