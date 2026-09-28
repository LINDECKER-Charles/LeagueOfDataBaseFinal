import { fetchUpstream, TransientUpstreamError } from './fetch-upstream.mjs';

/**
 * The responses captured so far, one per URL. A transient failure is kept aside instead:
 * the run then writes nothing at all, so the recorded set is always a whole recording.
 */
export class Recording {
  #fetchImpl;
  #limit;
  #pending = new Map();
  entries = new Map();
  failures = [];

  constructor({ fetchImpl = fetch, concurrency = 8 } = {}) {
    this.#fetchImpl = fetchImpl;
    this.#limit = limiter(concurrency);
  }

  /**
   * Records a JSON document reduced by `reduce`, which returns `{document, reduction}`.
   * @returns the reduced document, or null when the upstream has none or failed
   */
  async json(url, reduce = (document) => ({ document })) {
    const response = await this.#fetch(url);
    if (response?.body === undefined) {
      return null;
    }
    const { document, reduction } = reduce(JSON.parse(response.body.toString('utf8')));
    const body = Buffer.from(JSON.stringify(document), 'utf8');
    this.entries.set(url, { ...response, body, reduction });
    return document;
  }

  /** Records a response as the upstream sent it (images). */
  async raw(url) {
    const response = await this.#fetch(url);
    if (response) {
      this.entries.set(url, response);
    }
  }

  /** Records only the status of a response whose body no test reads. */
  async status(url, reason) {
    const response = await this.#fetch(url);
    if (response) {
      const { body, ...head } = response;
      const bodyless = body !== undefined;
      this.entries.set(url, bodyless ? { ...head, bodyless, reduction: reason } : head);
    }
  }

  /** The same URL is fetched once, whichever phase asks for it first. */
  #fetch(url) {
    if (!this.#pending.has(url)) {
      this.#pending.set(url, this.#limit(() => this.#settle(url)));
    }
    return this.#pending.get(url);
  }

  async #settle(url) {
    try {
      const response = await fetchUpstream(url, this.#fetchImpl);
      if (response.body === undefined) {
        this.entries.set(url, response);
      }
      return response;
    } catch (error) {
      if (!(error instanceof TransientUpstreamError)) {
        throw error;
      }
      this.failures.push(error);
      return null;
    }
  }
}

function limiter(concurrency) {
  let active = 0;
  const queue = [];
  const next = () => {
    if (active >= concurrency || queue.length === 0) {
      return;
    }
    active++;
    const { task, resolve, reject } = queue.shift();
    task()
      .then(resolve, reject)
      .finally(() => {
        active--;
        next();
      });
  };
  return (task) =>
    new Promise((resolve, reject) => {
      queue.push({ task, resolve, reject });
      next();
    });
}
