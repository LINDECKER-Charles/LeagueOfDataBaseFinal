import { promisify } from 'node:util';
import { gzip } from 'node:zlib';
import { acceptsGzip } from './accepts-gzip';

// On the libuv thread pool: compressing a page never blocks the renders.
const gzipAsync = promisify(gzip);
const HTML = /^text\/html\b/i;
const VARY = 'Vary';
const ACCEPT_ENCODING = 'Accept-Encoding';

function withVaryOnEncoding(source: Headers): Headers {
  const headers = new Headers(source);
  const vary = headers.get(VARY) ?? '';
  if (!vary.toLowerCase().includes(ACCEPT_ENCODING.toLowerCase())) {
    headers.append(VARY, ACCEPT_ENCODING);
  }
  return headers;
}

/**
 * The document of a render as the client asked for it: compressed once, here, at `level`
 * when its Accept-Encoding admits gzip, which nginx always sends so as to keep one compressed
 * copy for every client; as it is otherwise. Either way it varies on Accept-Encoding.
 * Anything but an HTML document, or a body already encoded, goes through untouched.
 */
export async function encodedDocument(
  document: Response,
  acceptEncoding: string | undefined,
  level: number,
): Promise<Response> {
  const type = document.headers.get('Content-Type') ?? '';
  if (!HTML.test(type) || document.body === null || document.headers.has('Content-Encoding')) {
    return document;
  }
  const init = { status: document.status, statusText: document.statusText };
  const headers = withVaryOnEncoding(document.headers);
  if (!acceptsGzip(acceptEncoding)) {
    return new Response(document.body, { ...init, headers });
  }
  const compressed = await gzipAsync(await document.arrayBuffer(), { level });
  headers.set('Content-Encoding', 'gzip');
  headers.set('Content-Length', String(compressed.byteLength));
  return new Response(compressed, { ...init, headers });
}
