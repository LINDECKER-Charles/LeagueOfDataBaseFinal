import { inlineScripts } from './inline-scripts';

describe('inlineScripts', () => {
  it.each([
    '<script>alert(1)</script>',
    '<script type="text/javascript" id="ng-event-dispatch-contract">alert(1)</script>',
    "<script type='module'>alert(1)</script>",
    '<script type=application/javascript>alert(1)</script>',
    '<SCRIPT data-src="x">alert(1)</SCRIPT >',
    '<script>alert(1)</script\t\nfoo="bar">',
  ])('finds the executable script %s', (html) => {
    expect(inlineScripts(html).map((script) => script.text)).toEqual(['alert(1)']);
  });

  it.each([
    '<script src="/build/main-X2Q5VODG.js" type="module"></script>',
    '<script type="application/ld+json">{"@context":"https://schema.org"}</script>',
    '<script id="ng-state" type="application/json">{"a":"\u003C/script>"}</script>',
    '<script type="text/template">alert(1)</script>',
  ])('leaves out %s, which runs no inline code', (html) => {
    expect(inlineScripts(html)).toEqual([]);
  });

  it('keeps the exact text, whitespace included, in document order', () => {
    const html = '<head><script>\n  paint();\n</script></head><script>alert(1)</script>';

    expect(inlineScripts(html).map((script) => script.text)).toEqual([
      '\n  paint();\n',
      'alert(1)',
    ]);
  });

  it('tells where each script starts and ends', () => {
    const html = '<p></p><script id="a">one()</script><script>two()</script>';

    const [first, second] = inlineScripts(html);

    expect(first).toEqual({ attributes: ' id="a"', text: 'one()', start: 7, end: 36 });
    expect(second.start).toBe(first.end);
    expect(second.end).toBe(html.length);
  });

  it('finds nothing in a page without scripts', () => {
    expect(inlineScripts('<!doctype html><title>Offline</title>')).toEqual([]);
  });
});
