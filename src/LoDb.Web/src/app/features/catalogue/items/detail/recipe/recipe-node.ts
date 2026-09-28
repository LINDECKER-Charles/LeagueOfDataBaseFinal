import type { CatalogImage } from '../../../../../core/api/generated/models/catalog-image';

/** An item of the recipe tree as the page draws it. */
export interface RecipeNode {
  readonly id: string;
  /** Its name, or its id when Data Dragon names it not. */
  readonly name: string;
  readonly image: CatalogImage;
  /** Its page; null for the root, which is the page itself. */
  readonly canonicalPath: string | null;
  /** Total cost. */
  readonly gold: number;
  /** What completing it costs on top of its components; null when there is nothing to add. */
  readonly combine: number | null;
  readonly components: readonly RecipeNode[];
}
