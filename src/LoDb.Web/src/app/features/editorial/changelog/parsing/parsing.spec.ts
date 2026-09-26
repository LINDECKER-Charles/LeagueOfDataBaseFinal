import { formatReleaseDate } from '../format-release-date';
import { releaseAnchor } from '../release-anchor';
import { parseManifest } from './parse-manifest';
import { parseRelease } from './parse-release';
// The manifest the build copies (angular.json), where the release tooling writes it.
import published from '../published/manifest.json';

describe('parseManifest', () => {
  it('keeps the entries in order, with their version', () => {
    const manifest = {
      patches: [
        { id: '2026-08-22-tamis', version: '2.2.1', codename: 'Tamis' },
        { id: '2026-08-22-jade', version: '2.2.0' },
      ],
    };

    expect(parseManifest(manifest)).toEqual([
      { id: '2026-08-22-tamis', version: '2.2.1' },
      { id: '2026-08-22-jade', version: '2.2.0' },
    ]);
  });

  it('keeps an entry without a version, with a null one', () => {
    expect(parseManifest({ patches: [{ id: 'a' }] })).toEqual([{ id: 'a', version: null }]);
  });

  it.each([
    ['no id', { version: '1.0.0' }],
    ['an empty id', { id: '', version: '1.0.0' }],
    ['a path', { id: '../secrets', version: '1.0.0' }],
    ['a query', { id: 'a?b=1', version: '1.0.0' }],
    ['not an object', '2026-08-22-jade'],
  ])('skips an entry with %s', (_, entry) => {
    expect(parseManifest({ patches: [entry, { id: 'kept' }] })).toEqual([
      { id: 'kept', version: null },
    ]);
  });

  it.each([null, 'manifest', [], {}, { patches: 'none' }])(
    'reads %j as an empty history',
    (json) => {
      expect(parseManifest(json)).toEqual([]);
    },
  );
});

describe('parseRelease', () => {
  const full = {
    id: '2026-08-22-jade',
    version: '2.2.0',
    codename: 'Jade',
    date: '2026-08-22',
    type: 'major',
    summary: 'Classic arrives.',
    intro: 'Jade is the Classic patch.',
    features: [
      { kind: 'mana', glyph: '⚡', tag: 'Images', title: 'Images', description: 'Loading.' },
      { kind: 'feature', title: 'Filters' },
      { description: 'No title: dropped.' },
    ],
    bugfixes: [{ id: 'LDB-019', area: 'Champions', text: 'Fiddlesticks.' }, { text: 'Bare.' }, {}],
    balances: [{ text: 'Not shown.' }],
    devNote: { body: 'Thanks.', signature: 'Charles' },
  };

  it('reads every block of a release file', () => {
    expect(parseRelease(full)).toEqual({
      version: '2.2.0',
      codename: 'Jade',
      date: '2026-08-22',
      type: 'major',
      summary: 'Classic arrives.',
      intro: 'Jade is the Classic patch.',
      features: [
        { kind: 'mana', glyph: '⚡', tag: 'Images', title: 'Images', description: 'Loading.' },
        { kind: 'feature', glyph: '◆', tag: null, title: 'Filters', description: '' },
      ],
      fixes: [
        { id: 'LDB-019', area: 'Champions', text: 'Fiddlesticks.' },
        { id: null, area: null, text: 'Bare.' },
      ],
      devNote: { body: 'Thanks.', signature: 'Charles' },
    });
  });

  it('leaves out what a minimal file does not carry', () => {
    expect(parseRelease({ version: '1.0.0' })).toEqual({
      version: '1.0.0',
      codename: '',
      date: '',
      type: 'minor',
      summary: '',
      intro: null,
      features: [],
      fixes: [],
      devNote: null,
    });
  });

  it.each(['patch', null, 3])('reads a type %j as a minor release', (type) => {
    expect(parseRelease({ version: '1.0.0', type })?.type).toBe('minor');
  });

  it('drops a developer note without a body', () => {
    expect(parseRelease({ version: '1.0.0', devNote: { signature: 'C' } })?.devNote).toBeNull();
  });

  it.each([null, [], 'release', { codename: 'No version' }, { version: '' }])(
    'rejects %j, which has no version to show',
    (json) => {
      expect(parseRelease(json)).toBeNull();
    },
  );
});

describe('the published manifest', () => {
  it('reads as a history whose newest entry carries the release version', () => {
    const entries = parseManifest(published);

    expect(entries.length).toBe(published.patches.length);
    expect(entries[0]?.version).toMatch(/^\d+\.\d+\.\d+$/);
  });
});

describe('formatReleaseDate', () => {
  it.each([
    ['2026-08-22', '22/08/2026'],
    ['2026-07-01', '01/07/2026'],
    ['summer 2026', 'summer 2026'],
    ['', ''],
  ])('prints %j as %j', (date, printed) => {
    expect(formatReleaseDate(date)).toBe(printed);
  });
});

describe('releaseAnchor', () => {
  it.each([
    ['2.2.1', 'v2-2-1'],
    ['1.0.0', 'v1-0-0'],
  ])('anchors %s at #%s', (version, anchor) => {
    expect(releaseAnchor(version)).toBe(anchor);
  });
});
