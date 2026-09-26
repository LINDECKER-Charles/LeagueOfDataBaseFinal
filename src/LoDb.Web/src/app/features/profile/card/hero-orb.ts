import type { FavoriteSlot } from '../../../core/api/generated/models/favorite-slot';

/** A favorite drifting over the hero of a public card. */
export interface HeroOrb {
  readonly slot: FavoriteSlot;
  /** Null for an image the catalogue lacks: the orb shows the initials. */
  readonly image: string | null;
  readonly initials: string;
  /** The champion's orb, larger than the others. */
  readonly primary: boolean;
  /** Its place among the four scattered around the name, from 1. */
  readonly position: number;
}
