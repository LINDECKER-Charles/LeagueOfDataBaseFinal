import type { ResourceType } from '../../api/generated/models/resource-type';
import { RESOURCE_TYPE } from '../../api/generated/models/resource-type-array';
import { isCatalogueRoute } from '../../routing/url/is-catalogue-route';
import { parsePublicUrl } from '../../routing/url/parse-public-url';

/**
 * The lists a destination shows, whose images a warm-up fetches before the visit: all four
 * on the home page, which previews each; its own on a list page; none elsewhere, a detail
 * page resolving its images before it answers. `isVersion` comes from `/api/meta`.
 */
export function resourcesOf(url: string, isVersion: (segment: string) => boolean): ResourceType[] {
  const { locale, page } = parsePublicUrl(url, isVersion);
  if (locale === null) {
    return [];
  }
  if (page.length === 0) {
    return [...RESOURCE_TYPE];
  }
  return page.length === 1 && isCatalogueRoute(page) ? [page[0] as ResourceType] : [];
}
