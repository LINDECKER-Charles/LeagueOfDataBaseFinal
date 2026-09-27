import type { CatalogMeta } from '../../../core/api/generated/models/catalog-meta';
import type { Preferences } from '../../../core/context/preferences/preferences';
import type { ContextTarget } from '../../../core/context/switch/context-target';

/**
 * What a switch keeps: the locale, the language variant and the version, the last two null
 * for the default. Choosing the latest keeps following the latest, not today's number,
 * which the next patch would turn into a pinned version.
 */
export function preferencesOf(target: ContextTarget, meta: CatalogMeta): Preferences {
  const version = target.version ?? null;
  const kept = { lang: target.lang ?? null, version: version === meta.latest ? null : version };
  return target.locale === undefined ? kept : { ...kept, locale: target.locale };
}
