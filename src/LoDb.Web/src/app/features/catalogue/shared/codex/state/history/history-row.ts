import type { CanonicalLink } from '../../../../../../core/api/generated/models/canonical-link';

/** A state of the entity in the block « Historique », newest first. */
export interface HistoryRow {
  /** Its run of patches: "Patches 11.5.1 to 11.9.1", "Patch 11.5.1", "Since patch 11.5.1". */
  readonly range: string;
  /** Its canonical: the opener of a closed state, the short URL of the open one. */
  readonly link: CanonicalLink;
  /** "New" or "Back", for a state that opened that way; null for a change. */
  readonly kind: string | null;
  /** The labels of the fields its opener changed, as one phrase; '' when none. */
  readonly fields: string;
  /** Whether it is the state the page shows. */
  readonly isPage: boolean;
}
