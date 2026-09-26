import type { JsonLdLink } from '../core/json-ld-link';
import type { JsonLdNode } from '../core/json-ld-node';
import { SCHEMA_ORG } from '../core/schema-org';

/** Enough entries for rich results, without bloating the render of a 600-item list. */
const ITEM_LIST_MAX = 20;

/** The first entries of a list page, a profile's builds or the trends. */
export function itemList(entries: readonly JsonLdLink[]): JsonLdNode {
  const listed = entries.slice(0, ITEM_LIST_MAX);
  return {
    '@context': SCHEMA_ORG,
    '@type': 'ItemList',
    numberOfItems: listed.length,
    itemListElement: listed.map((entry, index) => ({
      '@type': 'ListItem',
      position: index + 1,
      name: entry.name,
      url: entry.url,
    })),
  };
}
