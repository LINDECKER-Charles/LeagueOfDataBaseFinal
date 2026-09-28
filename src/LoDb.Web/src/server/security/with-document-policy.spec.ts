import { withDocumentPolicy } from './with-document-policy';

const PAGE = '<!doctype html><html><head><script>alert(1)</script></head><body></body></html>';

describe('withDocumentPolicy', () => {
  it('adds the policy of the page, with the hash of its inline script', async () => {
    const rendered = new Response(PAGE, {
      status: 404,
      headers: { 'Content-Type': 'text/html;charset=UTF-8', 'Cache-Control': 'public, max-age=0' },
    });

    const sent = await withDocumentPolicy(rendered);

    expect(sent.status).toBe(404);
    expect(sent.headers.get('Cache-Control')).toBe('public, max-age=0');
    expect(sent.headers.get('Content-Security-Policy')).toMatch(
      /script-src [^;]*'sha256-bhHHL3z2vDgxUt0W3dWQOrprscmda2Y5pLsLg4GF\+pI='/,
    );
    expect(await sent.text()).toBe(PAGE);
  });

  it('keeps the Location of a redirect', async () => {
    const rendered = new Response('<p>Moved</p>', {
      status: 301,
      headers: { 'Content-Type': 'text/html', Location: '/en/champions/Ahri' },
    });

    const sent = await withDocumentPolicy(rendered);

    expect(sent.status).toBe(301);
    expect(sent.headers.get('Location')).toBe('/en/champions/Ahri');
  });

  it('leaves anything but HTML untouched', async () => {
    const rendered = new Response('{}', { headers: { 'Content-Type': 'application/json' } });

    expect(await withDocumentPolicy(rendered)).toBe(rendered);
  });

  it('leaves a response without body untouched', async () => {
    const rendered = new Response(null, { status: 204, headers: { 'Content-Type': 'text/html' } });

    expect(await withDocumentPolicy(rendered)).toBe(rendered);
  });
});
