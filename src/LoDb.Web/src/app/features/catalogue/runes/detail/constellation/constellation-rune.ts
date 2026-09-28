import type { CatalogImage } from '../../../../../core/api/generated/models/catalog-image';

/** A rune as its path's constellation shows it. */
export interface ConstellationRune {
  readonly id: number;
  readonly key: string;
  readonly name: string;
  readonly image: CatalogImage;
  /** Id of its card, the target of the list's links: `rune-{key}`. */
  readonly anchor: string;
  /** Riot's rich text shown on the card. */
  readonly summary: string;
  /** The long description, behind a disclosure, when it says more than the summary. */
  readonly details: string | null;
}
