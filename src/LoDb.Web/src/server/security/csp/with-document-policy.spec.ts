import { KnownInlineScripts } from './scripts/known-inline-scripts';
import type { ScriptPolicy } from './scripts/script-policy';
import { withDocumentPolicy } from './with-document-policy';

const THEME_HASH = "'sha256-bhHHL3z2vDgxUt0W3dWQOrprscmda2Y5pLsLg4GF+pI='";
const TEMPLATE = '<head><script>alert(1)</script></head>';
const PAGE = `<!doctype html><html>${TEMPLATE}<body><p>Ahri</p></body></html>`;

function policyWith(onUnexpected: (count: number) => void = () => undefined): ScriptPolicy {
  return { known: new KnownInlineScripts([TEMPLATE]), onUnexpected };
}

function scriptSourcesOf(sent: Response): string | undefined {
  return /script-src ([^;]*)/.exec(sent.headers.get('Content-Security-Policy') ?? '')?.[1];
}

function rendered(body: string, headers: Record<string, string>, status = 200): Response {
  return new Response(body, { status, headers });
}

describe('withDocumentPolicy', () => {
  it('adds the policy of the page, with the hash of its known inline script', async () => {
    const page = rendered(
      PAGE,
      { 'Content-Type': 'text/html;charset=UTF-8', 'Cache-Control': 'public, max-age=0' },
      404,
    );

    const sent = await withDocumentPolicy(page, policyWith());

    expect(sent.status).toBe(404);
    expect(sent.headers.get('Cache-Control')).toBe('public, max-age=0');
    expect(scriptSourcesOf(sent)).toBe(`'self' ${THEME_HASH}`);
    expect(await sent.text()).toBe(PAGE);
  });

  it('reports the Trusted Types of the page without enforcing them', async () => {
    const sent = await withDocumentPolicy(
      rendered(PAGE, { 'Content-Type': 'text/html' }),
      policyWith(),
    );

    expect(sent.headers.get('Content-Security-Policy-Report-Only')).toContain(
      "require-trusted-types-for 'script'",
    );
    expect(sent.headers.get('Content-Security-Policy')).not.toContain('trusted-types');
    expect(sent.headers.get('Reporting-Endpoints')).toBe('csp="/api/csp-reports"');
  });

  it('leaves out and reports an inline script the build never wrote', async () => {
    const onUnexpected = vi.fn<(count: number) => void>();
    const injected = PAGE.replace('<p>Ahri</p>', '<p>Ahri<script>steal()</script></p>');

    const sent = await withDocumentPolicy(
      rendered(injected, { 'Content-Type': 'text/html' }),
      policyWith(onUnexpected),
    );

    expect(scriptSourcesOf(sent)).toBe(`'self' ${THEME_HASH}`);
    expect(onUnexpected).toHaveBeenCalledExactlyOnceWith(1);
  });

  it('reports nothing for a page whose scripts are all known', async () => {
    const onUnexpected = vi.fn<(count: number) => void>();

    await withDocumentPolicy(
      rendered(PAGE, { 'Content-Type': 'text/html' }),
      policyWith(onUnexpected),
    );

    expect(onUnexpected).not.toHaveBeenCalled();
  });

  it('keeps the Location of a redirect', async () => {
    const page = rendered(
      '<p>Moved</p>',
      {
        'Content-Type': 'text/html',
        Location: '/en/champions/Ahri',
      },
      301,
    );

    const sent = await withDocumentPolicy(page, policyWith());

    expect(sent.status).toBe(301);
    expect(sent.headers.get('Location')).toBe('/en/champions/Ahri');
  });

  it('leaves anything but HTML untouched', async () => {
    const json = rendered('{}', { 'Content-Type': 'application/json' });

    expect(await withDocumentPolicy(json, policyWith())).toBe(json);
  });

  it('leaves a response without body untouched', async () => {
    const empty = new Response(null, { status: 204, headers: { 'Content-Type': 'text/html' } });

    expect(await withDocumentPolicy(empty, policyWith())).toBe(empty);
  });
});
