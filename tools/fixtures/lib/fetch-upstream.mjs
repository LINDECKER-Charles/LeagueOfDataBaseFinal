// One GET with the verdicts of the ingestion's egress: 2xx is a body, 403/404 a definitive
// absence, anything else transient. A transient answer is retried, then reported, never
// recorded: an outage must not become a fixture.

const ABSENT_STATUSES = new Set([403, 404]);
const MAX_ATTEMPTS = 3;
const FIRST_BACKOFF_MS = 500;
const ATTEMPT_TIMEOUT_MS = 30_000;

/** A failure that is neither a body nor a definitive absence. */
export class TransientUpstreamError extends Error {
  constructor(url, reason) {
    super(`${url}: ${reason}`);
    this.name = 'TransientUpstreamError';
    this.url = url;
  }
}

/**
 * @returns {Promise<{url: string, status: number, contentType?: string, body?: Buffer}>}
 *   the body only for a 2xx
 * @throws {TransientUpstreamError} when every attempt failed without a verdict
 */
export async function fetchUpstream(url, fetchImpl = fetch) {
  let lastReason = 'no attempt';
  for (let attempt = 1; attempt <= MAX_ATTEMPTS; attempt++) {
    if (attempt > 1) {
      await delay(FIRST_BACKOFF_MS * 2 ** (attempt - 2));
    }
    try {
      return await attemptOnce(url, fetchImpl);
    } catch (error) {
      lastReason = error instanceof AttemptError ? error.message : String(error?.message ?? error);
    }
  }
  throw new TransientUpstreamError(url, lastReason);
}

class AttemptError extends Error {}

async function attemptOnce(url, fetchImpl) {
  const response = await fetchImpl(url, { signal: AbortSignal.timeout(ATTEMPT_TIMEOUT_MS) });
  if (ABSENT_STATUSES.has(response.status)) {
    await response.body?.cancel();
    return { url, status: response.status };
  }
  if (!response.ok) {
    await response.body?.cancel();
    throw new AttemptError(`answered ${response.status}`);
  }
  const body = Buffer.from(await response.arrayBuffer());
  const contentType = response.headers.get('content-type') ?? undefined;
  return { url, status: response.status, contentType, body };
}

const delay = (ms) => new Promise((resolve) => setTimeout(resolve, ms));
