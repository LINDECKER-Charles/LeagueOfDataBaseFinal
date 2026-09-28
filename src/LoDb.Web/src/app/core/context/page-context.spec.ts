import type { CatalogMeta } from '../api/generated/models/catalog-meta';
import type { ContextSources } from './context-sources';
import type { PageContext } from './page-context';
import { pageContextOf } from './page-context-of';

const LATEST = '16.19.1';
const OLDER = '15.14.1';
const META: CatalogMeta = {
  latest: LATEST,
  versions: [LATEST, '16.18.1', OLDER],
  readyVersions: [LATEST],
  versionPattern: String.raw`\d+(?:\.\d+)+`,
  languages: ['en_US', 'en_GB', 'fr_FR', 'ja_JP'],
  languagePattern: '[a-z]{2}_[A-Z]{2}',
  defaultLanguage: 'en_US',
  locales: [
    { locale: 'en', language: 'en_US' },
    { locale: 'fr', language: 'fr_FR' },
  ],
  fallbackLocale: 'en',
  gameModes: [{ mode: 'sr', map: 11 }],
  defaultGameMode: 'sr',
};

interface Case {
  readonly rule: string;
  readonly sources: Partial<ContextSources>;
  readonly expected: Partial<PageContext>;
}

const CASES: Case[] = [
  {
    rule: 'follows the latest version in the locale language by default',
    sources: {},
    expected: { version: LATEST, pinned: false, language: 'en_US' },
  },
  {
    rule: 'reads the language a locale maps to',
    sources: { locale: 'fr' },
    expected: { locale: 'fr', language: 'fr_FR' },
  },
  {
    rule: 'reads the version of the path',
    sources: { path: OLDER },
    expected: { version: OLDER, pinned: true },
  },
  {
    rule: 'reads the version of the query outside the catalogue paths',
    sources: { query: `?version=${OLDER}` },
    expected: { version: OLDER, pinned: true },
  },
  {
    rule: 'lets the path win over the query',
    sources: { path: OLDER, query: '?version=16.18.1' },
    expected: { version: OLDER },
  },
  {
    rule: 'lets the query win over the cookie',
    sources: { query: '?version=16.18.1', remembered: { lang: null, version: OLDER } },
    expected: { version: '16.18.1' },
  },
  {
    rule: 'applies a remembered version when the URL names none',
    sources: { remembered: { lang: null, version: OLDER } },
    expected: { version: OLDER, pinned: true },
  },
  {
    rule: 'skips a version Data Dragon does not list',
    sources: { query: '?version=99.99.1', remembered: { lang: null, version: OLDER } },
    expected: { version: OLDER },
  },
  {
    rule: 'marks the latest version as not pinned, however named',
    sources: { query: `?version=${LATEST}` },
    expected: { version: LATEST, pinned: false },
  },
  {
    rule: 'reads a regional variant of the query',
    sources: { query: '?page=2&lang=en_GB' },
    expected: { language: 'en_GB' },
  },
  {
    rule: 'lets the query language win over the cookie',
    sources: { query: '?lang=en_US', remembered: { lang: 'en_GB', version: null } },
    expected: { language: 'en_US' },
  },
  {
    rule: 'applies a remembered variant',
    sources: { remembered: { lang: 'en_GB', version: null } },
    expected: { language: 'en_GB' },
  },
  {
    rule: 'never swaps the language of the locale',
    sources: { locale: 'fr', query: '?lang=ja_JP', remembered: { lang: 'en_GB', version: null } },
    expected: { language: 'fr_FR' },
  },
  {
    rule: 'ignores a language Data Dragon does not list',
    sources: { query: '?lang=en_XX' },
    expected: { language: 'en_US' },
  },
  {
    rule: 'ignores empty parameters',
    sources: { query: '?version=&lang=' },
    expected: { version: LATEST, language: 'en_US' },
  },
];

function sourcesOf(sources: Partial<ContextSources>): ContextSources {
  return { locale: 'en', path: null, query: '', remembered: null, ...sources };
}

describe('pageContextOf (ADR 0005: path > query > cookie)', () => {
  it.each(CASES)('$rule', ({ sources, expected }) => {
    expect(pageContextOf(sourcesOf(sources), META)).toMatchObject(expected);
  });

  it('gives a complete context', () => {
    expect(pageContextOf(sourcesOf({ locale: 'fr', path: OLDER }), META)).toEqual({
      locale: 'fr',
      version: OLDER,
      pinned: true,
      language: 'fr_FR',
    });
  });

  it('gives nothing before the first ingestion, unless the URL names a version', () => {
    const empty: CatalogMeta = { ...META, latest: null };

    expect(pageContextOf(sourcesOf({}), empty)).toBeNull();
    expect(pageContextOf(sourcesOf({ path: OLDER }), empty)).toMatchObject({ version: OLDER });
  });
});
