import { CookieJar } from './cookie-jar.mjs';

const UNSAFE = new Set(['POST', 'PUT', 'PATCH', 'DELETE']);
const TIMEOUT_MS = 60_000;

/**
 * One visitor of a site: its own cookies, redirects not followed. An unsafe request carries
 * the site's Origin, as a browser's would (the new API and Symfony's stateless CSRF both
 * check it), and the XSRF token when the new API issued one.
 */
export class SiteClient {
  constructor(base) {
    this.base = base;
    this.origin = new URL(base).origin;
    this.jar = new CookieJar();
  }

  async send(path, { method = 'GET', json, form, headers = {} } = {}) {
    const all = { 'Accept-Language': 'en', ...headers };
    const cookie = this.jar.header();
    if (cookie !== undefined) {
      all.Cookie = cookie;
    }
    let body;
    if (json !== undefined) {
      all['Content-Type'] = 'application/json';
      body = JSON.stringify(json);
    } else if (form !== undefined) {
      body = new URLSearchParams(form);
    }
    if (UNSAFE.has(method)) {
      all.Origin = this.origin;
      all.Referer = `${this.origin}/`;
      const xsrf = this.jar.get('XSRF-TOKEN');
      if (xsrf !== undefined) {
        all['X-XSRF-TOKEN'] = decodeURIComponent(xsrf);
      }
    }
    const response = await fetch(new URL(path, this.base), {
      method,
      headers: all,
      body,
      redirect: 'manual',
      signal: AbortSignal.timeout(TIMEOUT_MS),
    });
    this.jar.store(response);
    return { status: response.status, location: response.headers.get('location'), text: await response.text() };
  }
}
