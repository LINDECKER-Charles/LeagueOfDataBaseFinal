import type { CatalogMeta } from '../../../core/api/generated/models/catalog-meta';
import type { VersionOption } from './version-option';

/**
 * The versions the switcher offers: every version Data Dragon lists, newest first as
 * `/api/meta` orders them, the long tail included as before; the latest is marked.
 */
export function versionOptions(meta: CatalogMeta): VersionOption[] {
  return meta.versions.map((version) => ({ version, latest: version === meta.latest }));
}
