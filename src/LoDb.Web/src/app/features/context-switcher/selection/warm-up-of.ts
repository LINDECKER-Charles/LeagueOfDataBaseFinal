import type { CatalogMeta } from '../../../core/api/generated/models/catalog-meta';
import { versionMatcher } from '../../../core/api/meta/version-matcher';
import { resourcesOf } from '../../../core/context/warm-up/resources-of';
import type { WarmUpTarget } from '../../../core/context/warm-up/warm-up-target';

/**
 * What the loader warms before a switch lands on `next`: the chosen version in the chosen
 * Data Dragon language, and the lists that page shows.
 */
export function warmUpOf(
  next: string,
  choice: { readonly version: string; readonly language: string },
  meta: CatalogMeta,
): WarmUpTarget {
  const matcher = versionMatcher(meta);
  return { ...choice, resources: resourcesOf(next, (segment) => matcher.test(segment)) };
}
