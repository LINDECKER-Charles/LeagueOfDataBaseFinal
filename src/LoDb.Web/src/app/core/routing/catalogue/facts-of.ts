import type { CatalogMeta } from '../../api/generated/models/catalog-meta';
import type { CanonicalFacts } from '../canonical-facts';

/** What `/api/meta` says about a catalogue page, and the API about its entity once asked. */
export function factsOf(meta: CatalogMeta, canonicalPath?: string | null): CanonicalFacts {
  return { latest: meta.latest ?? null, versions: meta.versions, canonicalPath };
}
