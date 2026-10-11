import { KnownInlineScripts } from './known-inline-scripts';
import { scriptHash } from './script-hash';

const THEME = '\n      (function () { paint(); })();\n    ';
const CONTRACT = '(()=>{window.__jsaction_bootstrap=c;})();\n';
const LOADER = "document.querySelectorAll('link[data-beasties-media]')";
const REPLAY = 'window.__jsaction_bootstrap(document.body,"ng",["click","keydown"],[]);';
const CONTRACT_OPEN = '<script type="text/javascript" id="ng-event-dispatch-contract">';
const CONTRACT_TAG = `${CONTRACT_OPEN}${CONTRACT}</script>`;

// The two index templates of a build: the server render's and the browser's.
const SERVER_TEMPLATE = `<head><script>${THEME}</script></head><body>${CONTRACT_TAG}</body>`;
const BROWSER_TEMPLATE = `<head><script>${THEME}</script></head><body><script>${LOADER}</script>`;

function page(body: string): string {
  return `<!doctype html><html><head><script>${THEME}</script></head><body>${body}</body></html>`;
}

describe('KnownInlineScripts', () => {
  const known = new KnownInlineScripts([SERVER_TEMPLATE, BROWSER_TEMPLATE]);

  it('admits the scripts the build wrote into its templates', () => {
    const html = page(`<p>Ahri</p>${CONTRACT_TAG}<script>${LOADER}</script>`);

    expect(known.of(html)).toEqual({
      hashes: [scriptHash(THEME), scriptHash(CONTRACT), scriptHash(LOADER)],
      unexpected: 0,
    });
  });

  it('admits the event replay right after the contract', () => {
    const html = page(`${CONTRACT_TAG}<script>${REPLAY}</script>`);

    expect(known.of(html)).toEqual({
      hashes: [scriptHash(THEME), scriptHash(CONTRACT), scriptHash(REPLAY)],
      unexpected: 0,
    });
  });

  it.each([
    ['anywhere else', `${CONTRACT_TAG}<p></p><script>${REPLAY}</script>`],
    [
      'after a contract the build never wrote',
      `${CONTRACT_TAG.replace(CONTRACT, 'evil();')}<script>${REPLAY}</script>`,
    ],
    [
      'with other arguments',
      `${CONTRACT_TAG}<script>${REPLAY.replace('[]', '[alert(1)]')}</script>`,
    ],
    ['followed by more code', `${CONTRACT_TAG}<script>${REPLAY}alert(1);</script>`],
  ])('refuses an event replay %s', (_case, body) => {
    const scripts = known.of(page(body));

    expect(scripts.hashes).not.toContain(scriptHash(REPLAY));
    expect(scripts.unexpected).toBeGreaterThan(0);
  });

  // A script slipped into the HTML on the server: stored content, a template mistake.
  it('leaves out and counts any other inline script', () => {
    const html = page('<p>Ahri<script>alert(document.cookie)</script></p><script>x()</script>');

    expect(known.of(html)).toEqual({ hashes: [scriptHash(THEME)], unexpected: 2 });
  });

  it('lists each admitted script once', () => {
    const html = page(`<script>${LOADER}</script><script>${LOADER}</script>`);

    expect(known.of(html).hashes).toEqual([scriptHash(THEME), scriptHash(LOADER)]);
  });
});
