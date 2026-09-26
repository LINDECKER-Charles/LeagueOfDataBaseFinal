import { plainText } from './plain-text';
import { propertyValues } from './property-values';
import { pruneJsonLd } from './prune-json-ld';
import { serializeJsonLd } from './serialize-json-ld';

describe('serializeJsonLd', () => {
  it('can never close its script element nor open markup', () => {
    const text = serializeJsonLd({
      name: '</script><script>alert(1)</script>',
      description: '<!-- & -->',
    });

    expect(text).not.toMatch(/[<>&]/);
    expect(text).toBe(
      '{"name":"\\u003c/script\\u003e\\u003cscript\\u003ealert(1)\\u003c/script\\u003e",' +
        '"description":"\\u003c!-- \\u0026 --\\u003e"}',
    );
  });

  it('escapes the line separators JavaScript strings cannot hold', () => {
    expect(serializeJsonLd({ name: 'a\u2028b\u2029c' })).toBe('{"name":"a\\u2028b\\u2029c"}');
  });

  it('keeps slashes and native scripts readable, and parses back to the same value', () => {
    const node = { url: 'https://league-of-data-base.com/ja/', name: 'アーリ', title: '九尾の狐' };
    const text = serializeJsonLd(node);

    expect(text).toContain('https://league-of-data-base.com/ja/');
    expect(text).toContain('アーリ');
    expect(JSON.parse(text)).toEqual(node);
  });

  it('parses back to the original even when it had to escape', () => {
    const node = { name: '<b>Tom & Jerry</b>' };

    expect(JSON.parse(serializeJsonLd(node))).toEqual(node);
  });
});

describe('pruneJsonLd', () => {
  it('drops absent and empty members, and keeps false and zero', () => {
    expect(
      pruneJsonLd({ a: undefined, b: null, c: '', d: [], e: false, f: 0, g: 'x', h: [1] }),
    ).toEqual({ e: false, f: 0, g: 'x', h: [1] });
  });
});

describe('propertyValues', () => {
  it('states one PropertyValue per present value, with its unit', () => {
    expect(propertyValues('Total cost', [3000], 'gold')).toEqual([
      { '@type': 'PropertyValue', name: 'Total cost', value: 3000, unitText: 'gold' },
    ]);
    expect(propertyValues('Role', ['Mage', 'Assassin'])).toHaveLength(2);
  });

  it('states nothing for an absent or NaN value, and a boolean as text', () => {
    expect(propertyValues('Range', [undefined, null, '', Number.NaN])).toEqual([]);
    expect(propertyValues('Purchasable', [false])).toEqual([
      { '@type': 'PropertyValue', name: 'Purchasable', value: 'false' },
    ]);
  });
});

describe('plainText', () => {
  it('drops markup and collapses whitespace, or answers null', () => {
    expect(plainText('Gain <attention>30</attention>  Armor.<br><br>Unique')).toBe(
      'Gain 30 Armor. Unique',
    );
    expect(plainText('From <b>Data Dragon</b>.')).toBe('From Data Dragon.');
    expect(plainText('  <br>  ')).toBeNull();
    expect(plainText(undefined)).toBeNull();
  });
});
