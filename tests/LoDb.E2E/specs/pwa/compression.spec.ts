import { request as httpRequest, type IncomingHttpHeaders } from 'node:http';
import { gunzipSync } from 'node:zlib';
import { expect, test } from '../../support/test';

interface RawAnswer {
  readonly headers: IncomingHttpHeaders;
  readonly body: Buffer;
}

// A query nobody else sends: the page cache sees a key of its own on every run.
function freshKey(path: string): string {
  return `${path}?e2e=${Date.now()}-${Math.random().toString(36).slice(2)}`;
}

// The body as nginx sent it: Playwright's own client would decompress it.
function rawGet(url: URL, acceptEncoding: string): Promise<RawAnswer> {
  return new Promise((resolve, reject) => {
    const headers = { 'accept-encoding': acceptEncoding };
    const outgoing = httpRequest(url, { headers }, (response) => {
      const chunks: Buffer[] = [];
      response.on('data', (chunk: Buffer) => chunks.push(chunk));
      response.on('end', () => resolve({ headers: response.headers, body: Buffer.concat(chunks) }));
      response.on('error', reject);
    });
    outgoing.on('error', reject);
    outgoing.end();
  });
}

test.describe('compression of a page', { tag: '@readonly' }, () => {
  test('is done once, and the page is identical once decompressed', async ({ baseURL }) => {
    const url = new URL(freshKey('/en/about'), baseURL);

    const compressed = await rawGet(url, 'gzip, deflate, br');
    const plain = await rawGet(url, 'identity');

    expect(compressed.headers['x-cache-status']).toBe('MISS');
    expect(compressed.headers['content-encoding']).toBe('gzip');
    expect(plain.headers['x-cache-status']).toBe('HIT');
    expect(plain.headers['content-encoding']).toBeUndefined();
    expect(gunzipSync(compressed.body).equals(plain.body)).toBe(true);
    expect(plain.body.toString('utf8')).toContain('</html>');
    for (const answer of [compressed, plain]) {
      expect(answer.headers['vary']).toBe('Accept-Encoding');
    }
  });
});
