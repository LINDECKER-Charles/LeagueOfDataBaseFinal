import type { CatalogMeta } from '../../../core/api/generated/models/catalog-meta';
import { languageLabel, languageTag } from './language-label';
import { languageOptions } from './language-options';
import { versionOptions } from './version-options';

const LATEST = '16.19.1';
const META: CatalogMeta = {
  latest: LATEST,
  versions: [LATEST, '16.18.1', '15.14.1', '3.7.1'],
  readyVersions: [LATEST],
  versionPattern: String.raw`\d+(?:\.\d+)+`,
  languages: ['en_US', 'en_GB', 'fr_FR', 'zh_CN', 'zh_TW', 'zh_MY', 'es_ES', 'es_MX', 'nl_NL'],
  languagePattern: '[a-z]{2}_[A-Z]{2}',
  defaultLanguage: 'en_US',
  locales: [
    { locale: 'en', language: 'en_US' },
    { locale: 'fr', language: 'fr_FR' },
    { locale: 'zh-hans', language: 'zh_CN' },
    { locale: 'zh-hant', language: 'zh_TW' },
    { locale: 'es', language: 'es_ES' },
  ],
  fallbackLocale: 'en',
  gameModes: [{ mode: 'sr', map: 11 }],
  defaultGameMode: 'sr',
};

describe('versionOptions', () => {
  it('offers every listed version, newest first, the long tail included', () => {
    expect(versionOptions(META).map((option) => option.version)).toEqual(META.versions);
  });

  it('marks the latest version, and no other', () => {
    const latest = versionOptions(META).filter((option) => option.latest);

    expect(latest).toEqual([{ version: LATEST, latest: true }]);
  });

  it('marks nothing while no version is ingested', () => {
    const options = versionOptions({ ...META, latest: null, versions: [] });

    expect(options).toEqual([]);
  });
});

describe('languageOptions', () => {
  const keys = () => languageOptions(META).map((option) => option.key);

  it('lists each locale with its own language, then its regional variants', () => {
    expect(keys()).toEqual([
      'en:en_US',
      'en:en_GB',
      'fr:fr_FR',
      'zh-hans:zh_CN',
      'zh-hans:zh_MY',
      'zh-hant:zh_TW',
      'es:es_ES',
      'es:es_MX',
    ]);
  });

  it('carries no ?lang= for the own language and the variant for the others', () => {
    const [own, variant] = languageOptions(META);

    expect(own).toMatchObject({ locale: 'en', language: 'en_US', lang: null, tag: 'en-US' });
    expect(variant).toMatchObject({ locale: 'en', language: 'en_GB', lang: 'en_GB' });
  });

  it('leaves out a language that no locale of the site shares', () => {
    expect(keys().some((key) => key.endsWith('nl_NL'))).toBe(false);
  });

  it("never lists another locale's own language as a variant", () => {
    expect(keys().filter((key) => key.endsWith(':zh_TW'))).toEqual(['zh-hant:zh_TW']);
  });
});

describe('languageLabel', () => {
  it('names a language in that language itself', () => {
    expect(languageLabel('fr_FR')).toBe('Français (France)');
    expect(languageLabel('en_GB')).toBe('English (United Kingdom)');
  });

  it('turns a Data Dragon code into a BCP 47 tag', () => {
    expect(languageTag('zh_TW')).toBe('zh-TW');
  });

  it('falls back to the code when the tag is ill-formed', () => {
    expect(languageLabel('not a language')).toBe('not a language');
  });
});
