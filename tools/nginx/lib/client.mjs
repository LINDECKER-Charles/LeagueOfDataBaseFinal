// A plain HTTP/1.1 client for the checks: fetch() can neither set Host nor keep a 301, and it
// decodes a compressed body, which the checks compare as sent.
import { request } from 'node:http';

function headersOf({ host, body, agent, encoding }) {
  return {
    host,
    ...(body ? { 'content-length': body.length } : {}),
    ...(agent ? { 'user-agent': agent } : {}),
    ...(encoding ? { 'accept-encoding': encoding } : {}),
  };
}

/**
 * Sends one request to 127.0.0.1:<port>; resolves to its status, headers and body (a Buffer,
 * as sent). A request body is a Buffer; `encoding` is the Accept-Encoding sent, none by
 * default. nginx may answer 413 and close before the body is sent: the answer wins.
 */
export function send(port, path, { host = 'localhost', method = 'GET', body, agent, encoding }
  = {}) {
  return new Promise((resolve, reject) => {
    let answered = false;
    const headers = headersOf({ host, body, agent, encoding });
    const outgoing = request({ host: '127.0.0.1', port, path, method, headers }, (response) => {
      answered = true;
      const chunks = [];
      response.on('data', (chunk) => chunks.push(chunk));
      response.on('end', () => resolve({
        status: response.statusCode,
        headers: response.headers,
        body: Buffer.concat(chunks),
      }));
    });
    outgoing.on('error', (error) => answered || reject(error));
    outgoing.end(body);
  });
}

/** Sends the same request `count` times in a row; resolves to their statuses. */
export async function statusesOf(port, path, count, options = {}) {
  const statuses = [];
  for (let sent = 0; sent < count; sent += 1) {
    statuses.push((await send(port, path, options)).status);
  }
  return statuses;
}
