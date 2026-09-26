import type { CatalogMeta } from '../../../core/api/generated/models/catalog-meta';
import type { Preferences } from '../../../core/context/preferences/preferences';
import type { ContextTarget } from '../../../core/context/switch/context-target';

/**
 * What "remember" keeps of a switch: the language variant and the version, each null for
 * the default. Choosing the latest remembers to follow the latest, not today's number,
 * which the next patch would turn into a pinned version.
 */
export function preferencesOf(target: ContextTarget, meta: CatalogMeta): Preferences {
  const version = target.version ?? null;
  return { lang: target.lang ?? null, version: version === meta.latest ? null : version };
}
