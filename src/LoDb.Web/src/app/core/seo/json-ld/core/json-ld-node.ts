type JsonLdValue = string | number | boolean | null | JsonLdNode | readonly JsonLdValue[];

/**
 * A schema.org node as the builders return it. `undefined` members are allowed so builders
 * can list every field and let {@link pruneJsonLd} drop the absent ones.
 */
export interface JsonLdNode {
  readonly [property: string]: JsonLdValue | undefined;
}
