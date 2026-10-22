import type { CatalogMeta } from '../api/generated/models/catalog-meta';

// What a spec leaves unsaid: one version, read in en_US alone, and the patterns of the API;
// a single version has no page of changes.
const BASE: CatalogMeta = {
  latest: '16.19.1',
  versions: ['16.19.1'],
  readyVersions: ['16.19.1'],
  oldestChangesVersion: null,
  versionPattern: String.raw`\d+(?:\.\d+)+`,
  languages: ['en_US'],
  languagePattern: '[a-z]{2}_[A-Z]{2}',
  defaultLanguage: 'en_US',
  locales: [{ locale: 'en', language: 'en_US' }],
  fallbackLocale: 'en',
  gameModes: [{ mode: 'sr', map: 11 }],
  defaultGameMode: 'sr',
};

/**
 * An `/api/meta` for a spec: the fields it names, over a catalogue of 16.19.1 in en_US alone.
 * The latest version is the one ingested ahead, unless the spec says otherwise.
 */
export function catalogMetaOf(fields: Partial<CatalogMeta> = {}): CatalogMeta {
  const latest = fields.latest === undefined ? BASE.latest : fields.latest;
  return { ...BASE, readyVersions: latest ? [latest] : [], ...fields };
}
