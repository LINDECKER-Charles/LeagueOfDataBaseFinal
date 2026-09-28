import type { FacetDefinition } from '../facets/model/facet-definition';

/** What reading and writing a list's URL depend on: its facets and its default page size. */
export interface FilterUrlSpec {
  readonly schema: readonly FacetDefinition[];
  readonly defaultSize: number;
}
