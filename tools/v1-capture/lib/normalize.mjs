// Turns a raw HTTP exchange into its reference form: only the headers of the contract,
// the body parsed when it is JSON, and volatile values replaced by stable markers.
import { resetHorizonSeconds } from './settings.mjs';

// Order matters: references list headers in this order, whatever the server sent.
export const recordedHeaders = Object.freeze([
  'content-type',
  'x-content-type-options',
  'allow',
  'location',
  'access-control-allow-origin',
  'access-control-allow-methods',
  'access-control-allow-headers',
  'x-ratelimit-limit',
  'x-ratelimit-remaining',
  'x-ratelimit-reset',
]);

export const markers = Object.freeze({
  unixTime: '<unix-time>',
  invalidUnixTime: '<invalid-unix-time>',
  volatile: '<volatile>',
});

const resetHeader = 'x-ratelimit-reset';
const jsonMediaType = /^application\/(.+\+)?json\b/;

/**
 * Replaces X-RateLimit-Reset with a marker once it is checked to be a Unix time within
 * the bucket horizon of the request window: a wrong value stays visible in a diff.
 */
export function normalizeReset(value, window) {
  const seconds = Number(value);
  const earliest = window.from - 1;
  const latest = window.to + resetHorizonSeconds + 1;
  const valid = Number.isInteger(seconds) && seconds >= earliest && seconds <= latest;
  return valid ? markers.unixTime : markers.invalidUnixTime;
}

function pickHeaders(headers, window) {
  const picked = {};
  for (const name of recordedHeaders) {
    if (headers[name] !== undefined) {
      picked[name] = name === resetHeader
        ? normalizeReset(headers[name], window)
        : headers[name];
    }
  }
  return picked;
}

/** Parses JSON bodies; anything else (Go's plain-text 404/405) is kept as text. */
export function decodeBody(contentType, text) {
  if (text === '') {
    return {};
  }
  if (jsonMediaType.test(contentType ?? '')) {
    try {
      return { body: JSON.parse(text) };
    } catch {
      return { text };
    }
  }
  return { text };
}

/** Replaces the value at a dotted path (headers.x or body.a.0.b) with the marker. */
export function markVolatile(response, dottedPath) {
  const segments = dottedPath.split('.');
  const last = segments.pop();
  let node = response;
  for (const segment of segments) {
    node = node?.[segment];
  }
  if (node !== undefined && node !== null && last in node) {
    node[last] = markers.volatile;
  }
}

/**
 * @param {{ status: number, headers: Record<string, string>, text: string,
 *           window: { from: number, to: number } }} raw
 * @param {string[]} [volatilePaths]
 */
export function normalizeResponse(raw, volatilePaths = []) {
  const response = {
    status: raw.status,
    headers: pickHeaders(raw.headers, raw.window),
    ...decodeBody(raw.headers['content-type'], raw.text),
  };
  for (const dottedPath of volatilePaths) {
    markVolatile(response, dottedPath);
  }
  return response;
}
