import type { JsonLdLink } from '../core/json-ld-link';
import type { JsonLdNode } from '../core/json-ld-node';
import { SCHEMA_ORG } from '../core/schema-org';

/** The trail from the home to the current page, in that order. */
export function breadcrumbList(crumbs: readonly JsonLdLink[]): JsonLdNode {
  return {
    '@context': SCHEMA_ORG,
    '@type': 'BreadcrumbList',
    itemListElement: crumbs.map((crumb, index) => ({
      '@type': 'ListItem',
      position: index + 1,
      name: crumb.name,
      item: crumb.url,
    })),
  };
}
