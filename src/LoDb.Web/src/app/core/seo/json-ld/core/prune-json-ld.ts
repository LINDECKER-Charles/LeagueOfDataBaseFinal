import type { JsonLdNode } from './json-ld-node';

/**
 * Drops `undefined`, `null`, empty-string and empty-array members: an empty schema.org value
 * is invalid, an absent one is not. Shallow by design: a nested node is pruned by its own
 * builder before it is passed in.
 */
export function pruneJsonLd(node: JsonLdNode): JsonLdNode {
  return Object.fromEntries(
    Object.entries(node).filter(
      ([, value]) =>
        value !== undefined &&
        value !== null &&
        value !== '' &&
        !(Array.isArray(value) && value.length === 0),
    ),
  );
}
