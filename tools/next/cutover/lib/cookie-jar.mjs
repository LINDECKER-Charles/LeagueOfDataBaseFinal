/**
 * The cookies of one site, as a browser would send them back: enough for a sign-in followed by
 * a few requests. Attributes are ignored, and an expired cookie (Max-Age=0) is dropped.
 */
export class CookieJar {
  #cookies = new Map();

  /** Keeps what a response set. */
  store(response) {
    for (const line of response.headers.getSetCookie()) {
      const [pair, ...attributes] = line.split(';');
      const separator = pair.indexOf('=');
      const name = pair.slice(0, separator).trim();
      const expired = attributes.some((attribute) => /^\s*max-age=0\s*$/i.test(attribute));
      if (expired) {
        this.#cookies.delete(name);
      } else {
        this.#cookies.set(name, pair.slice(separator + 1).trim());
      }
    }
  }

  /** The value of a cookie, or undefined. */
  get(name) {
    return this.#cookies.get(name);
  }

  /** The Cookie header, or undefined when the jar is empty. */
  header() {
    if (this.#cookies.size === 0) {
      return undefined;
    }
    return [...this.#cookies].map(([name, value]) => `${name}=${value}`).join('; ');
  }
}
