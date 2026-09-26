import { inlineScriptHashes } from './inline-script-hashes';

describe('inlineScriptHashes', () => {
  const alertHash = "'sha256-bhHHL3z2vDgxUt0W3dWQOrprscmda2Y5pLsLg4GF+pI='";

  it('hashes the exact text of an inline script, whitespace included', () => {
    const html = '<head><script>\n  paint();\n</script></head>';

    expect(inlineScriptHashes(html)).toEqual([
      "'sha256-VZcnJh5czfs7brzk/5OzvveY+ipAveKJh/ljBdqVARA='",
    ]);
  });

  it.each([
    '<script>alert(1)</script>',
    '<script type="text/javascript" id="ng-event-dispatch-contract">alert(1)</script>',
    "<script type='module'>alert(1)</script>",
    '<script type=application/javascript>alert(1)</script>',
    '<SCRIPT data-src="x">alert(1)</SCRIPT >',
  ])('hashes the executable script %s', (html) => {
    expect(inlineScriptHashes(html)).toEqual([alertHash]);
  });

  it.each([
    '<script src="/build/main-X2Q5VODG.js" type="module"></script>',
    '<script type="application/ld+json">{"@context":"https://schema.org"}</script>',
    '<script id="ng-state" type="application/json">{"a":"\\u003C/script>"}</script>',
    '<script type="text/template">alert(1)</script>',
  ])('leaves out %s, which runs no inline code', (html) => {
    expect(inlineScriptHashes(html)).toEqual([]);
  });

  it('lists each script once, in document order', () => {
    const html =
      '<script>alert(1)</script><p></p><script>\n  paint();\n</script><script>alert(1)</script>';

    expect(inlineScriptHashes(html)).toEqual([
      alertHash,
      "'sha256-VZcnJh5czfs7brzk/5OzvveY+ipAveKJh/ljBdqVARA='",
    ]);
  });

  it('finds nothing in a page without scripts', () => {
    expect(inlineScriptHashes('<!doctype html><title>Offline</title>')).toEqual([]);
  });
});
