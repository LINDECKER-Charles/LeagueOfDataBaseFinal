import { gunzipSync } from 'node:zlib';
import { encodedDocument } from './encoded-document';

const LEVEL = 6;
const PAGE = `<!doctype html><html lang="fr"><body>${'<p>Ahri</p>'.repeat(500)}</body></html>`;
const POLICY = "default-src 'self'";

function rendered(body: string | null, headers: Record<string, string> = {}): Response {
  return new Response(body, {
    status: 404,
    statusText: 'Not Found',
    headers: { 'Content-Type': 'text/html; charset=utf-8', ...headers },
  });
}

async function bytesOf(response: Response): Promise<Buffer> {
  return Buffer.from(await response.arrayBuffer());
}

describe('encodedDocument', () => {
  it('compresses a page once for a client that takes gzip, identical once decompressed', async () => {
    const page = rendered(PAGE, { 'Content-Security-Policy': POLICY });

    const sent = await encodedDocument(page, 'gzip', LEVEL);
    const bytes = await bytesOf(sent);

    expect(sent.headers.get('Content-Encoding')).toBe('gzip');
    expect(sent.headers.get('Content-Length')).toBe(String(bytes.byteLength));
    expect(bytes.byteLength).toBeLessThan(PAGE.length / 10);
    expect(gunzipSync(bytes).toString('utf8')).toBe(PAGE);
  });

  it('keeps the status and the headers of the render, and varies on Accept-Encoding', async () => {
    const page = rendered(PAGE, { 'Content-Security-Policy': POLICY, 'Cache-Control': 'x' });

    const sent = await encodedDocument(page, 'gzip, br', LEVEL);

    expect(sent.status).toBe(404);
    expect(sent.statusText).toBe('Not Found');
    expect(sent.headers.get('Content-Security-Policy')).toBe(POLICY);
    expect(sent.headers.get('Cache-Control')).toBe('x');
    expect(sent.headers.get('Vary')).toBe('Accept-Encoding');
  });

  it('sends the page as it is to a client without gzip, still varying on it', async () => {
    const sent = await encodedDocument(rendered(PAGE), undefined, LEVEL);

    expect(sent.headers.get('Content-Encoding')).toBeNull();
    expect(sent.headers.get('Vary')).toBe('Accept-Encoding');
    expect(await sent.text()).toBe(PAGE);
  });

  it('adds to a Vary the render set, once', async () => {
    const languages = await encodedDocument(
      rendered(PAGE, { Vary: 'Accept-Language' }),
      'gzip',
      LEVEL,
    );
    const already = await encodedDocument(
      rendered(PAGE, { Vary: 'accept-encoding' }),
      'gzip',
      LEVEL,
    );

    expect(languages.headers.get('Vary')).toBe('Accept-Language, Accept-Encoding');
    expect(already.headers.get('Vary')).toBe('accept-encoding');
  });

  it.each([
    ['anything but HTML', rendered('{}', { 'Content-Type': 'application/json' })],
    ['a body already encoded', rendered(PAGE, { 'Content-Encoding': 'br' })],
    ['an answer without body', rendered(null)],
  ])('leaves %s untouched', async (_case, response) => {
    expect(await encodedDocument(response, 'gzip', LEVEL)).toBe(response);
  });
});
