import type { JsonLdNode } from './json-ld-node';
import { pruneJsonLd } from './prune-json-ld';

type PropertyInput = string | number | boolean | null | undefined;

/**
 * One schema.org `PropertyValue` per present value: a champion has several roles, an item
 * several categories, and consumers expect a repeated property for them. Absent and NaN
 * values yield nothing: an empty property is worse than none. schema.org has no vocabulary
 * for game statistics, and gold is not a currency: `unitText` states the unit instead.
 */
export function propertyValues(
  name: string,
  values: readonly PropertyInput[],
  unitText?: string,
): JsonLdNode[] {
  return values
    .filter((value) => value !== null && value !== undefined && value !== '')
    .filter((value) => !Number.isNaN(value))
    .map((value) =>
      pruneJsonLd({
        '@type': 'PropertyValue',
        name,
        value: typeof value === 'boolean' ? String(value) : value,
        unitText,
      }),
    );
}
