import { RESOURCE_TYPE } from '../../api/generated/models/resource-type-array';
import type { ResourceType } from '../../api/generated/models/resource-type';

/**
 * Whether a page (the segments below the locale and the version) is a list or a detail of
 * the catalogue: the only pages whose version lives in the path (ADR 0005).
 */
export function isCatalogueRoute(page: readonly string[]): boolean {
  return RESOURCE_TYPE.includes(page[0] as ResourceType);
}
