import type { CatalogMeta } from '../../api/generated/models/catalog-meta';
import type { ContextTarget } from './context-target';
import { switchContext } from './switch-context';

const LATEST = '16.14.1';
const META: CatalogMeta = {
  latest: LATEST,
  versions: [LATEST, '15.14.1'],
  readyVersions: [LATEST],
  versionPattern: String.raw`\d+(?:\.\d+)+`,
  languages: ['en_US', 'en_GB', 'fr_FR', 'de_DE', 'zh_CN', 'zh_TW'],
  languagePattern: '[a-z]{2}_[A-Z]{2}',
  defaultLanguage: 'en_US',
  locales: [
    { locale: 'en', language: 'en_US' },
    { locale: 'fr', language: 'fr_FR' },
    { locale: 'de', language: 'de_DE' },
    { locale: 'zh-hans', language: 'zh_CN' },
    { locale: 'zh-hant', language: 'zh_TW' },
  ],
  fallbackLocale: 'en',
  gameModes: [{ mode: 'sr', map: 11 }],
  defaultGameMode: 'sr',
};

interface Case {
  readonly rule: string;
  readonly from: string;
  readonly target: ContextTarget;
  readonly to: string;
}

// The five cases of the legacy urls.spec.ts (destinationForSwitch), under the locale prefix:
// a language that is the locale's own is implied, and the pagination is now page and size.
const LEGACY_CASES: Case[] = [
  {
    rule: 'pins a non-latest patch in the path of a resource route',
    from: '/fr/champions',
    target: { version: '15.14.1', lang: 'fr_FR' },
    to: '/fr/15.14.1/champions',
  },
  {
    rule: 'drops the path segment when switching back to the latest patch',
    from: '/fr/15.14.1/champions',
    target: { version: LATEST, lang: 'fr_FR' },
    to: '/fr/champions',
  },
  {
    rule: 'falls back to the query outside the versioned routes',
    from: '/en/',
    target: { version: '15.14.1', lang: 'en_GB' },
    to: '/en/?lang=en_GB&version=15.14.1',
  },
  {
    rule: 'keeps the reader on their page across the switch',
    from: '/fr/champions?page=4&size=48',
    target: { version: '15.14.1', lang: 'fr_FR' },
    to: '/fr/15.14.1/champions?page=4&size=48',
  },
  {
    rule: 'carries the list filters across the switch and drops the old selection',
    from: '/en/15.14.1/items?tag=Boots%2CArmor&price=0-3000&lang=en_US&version=15.14.1',
    target: { version: LATEST, lang: 'en_GB' },
    to: '/en/items?tag=Boots%2CArmor&price=0-3000&lang=en_GB',
  },
];

const CASES: Case[] = [
  {
    rule: 'keeps a regional variant in the query, with the version in the path',
    from: '/en/champions/Aatrox',
    target: { version: '15.14.1', lang: 'en_GB' },
    to: '/en/15.14.1/champions/Aatrox?lang=en_GB',
  },
  {
    rule: 'switches the locale and keeps the page, its slug and its version',
    from: '/fr/15.14.1/items/1036-long-sword',
    target: { locale: 'de' },
    to: '/de/15.14.1/items/1036-long-sword',
  },
  {
    rule: 'drops a language that becomes the new locale own',
    from: '/en/champions?lang=fr_FR',
    target: { locale: 'fr' },
    to: '/fr/champions',
  },
  {
    rule: 'keeps a variant chosen before across a locale switch',
    from: '/en/champions/Aatrox?lang=en_GB',
    target: { locale: 'fr' },
    to: '/fr/champions/Aatrox?lang=en_GB',
  },
  {
    rule: 'switches the home page between the two Chinese locales',
    from: '/zh-hant/',
    target: { locale: 'zh-hans' },
    to: '/zh-hans/',
  },
  {
    rule: 'keeps the query version of a page outside the catalogue across a locale switch',
    from: '/fr/trends?version=15.14.1',
    target: { locale: 'en' },
    to: '/en/trends?version=15.14.1',
  },
  {
    rule: 'returns to the latest with a null version',
    from: '/fr/15.14.1/champions?page=2',
    target: { version: null },
    to: '/fr/champions?page=2',
  },
  {
    rule: 'returns to the locale language with a null lang',
    from: '/fr/champions?lang=en_GB',
    target: { lang: null },
    to: '/fr/champions',
  },
  {
    rule: 'keeps the pinned version when only the language changes',
    from: '/fr/15.14.1/champions',
    target: { lang: 'en_US' },
    to: '/fr/15.14.1/champions?lang=en_US',
  },
  {
    rule: 'moves a query version of a catalogue page into its path',
    from: '/fr/runes?version=15.14.1',
    target: { lang: 'fr_FR' },
    to: '/fr/15.14.1/runes',
  },
  {
    rule: 'lets the path win over the query',
    from: '/fr/15.14.1/runes?version=16.1.1',
    target: {},
    to: '/fr/15.14.1/runes',
  },
  {
    rule: 'replaces a version the API does not list',
    from: '/fr/99.99.1/champions',
    target: { version: '15.14.1' },
    to: '/fr/15.14.1/champions',
  },
  {
    rule: 'keeps the fragment',
    from: '/fr/about#sources',
    target: { lang: 'en_GB' },
    to: '/fr/about?lang=en_GB#sources',
  },
  {
    rule: 'ignores empty parameters',
    from: '/fr/champions?lang=&version=',
    target: {},
    to: '/fr/champions',
  },
  {
    rule: 'leaves the locale of a page outside the locales alone',
    from: '/b/Zx81',
    target: { locale: 'fr', version: '15.14.1' },
    to: '/b/Zx81?version=15.14.1',
  },
  {
    rule: 'changes nothing without a target',
    from: '/fr/15.14.1/items?tag=Boots',
    target: {},
    to: '/fr/15.14.1/items?tag=Boots',
  },
];

describe('switchContext', () => {
  it.each(LEGACY_CASES)('$rule (legacy)', ({ from, target, to }) => {
    expect(switchContext(from, target, META)).toBe(to);
  });

  it.each(CASES)('$rule', ({ from, target, to }) => {
    expect(switchContext(from, target, META)).toBe(to);
  });
});
