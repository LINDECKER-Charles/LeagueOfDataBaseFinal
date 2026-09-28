/** A favorite slot as the editor holds it: the id it saves, and what it shows. */
export interface FavoriteChoice {
  /** The id saved; null for an empty slot. A favorite the patch lacks keeps its id. */
  readonly id: string | null;
  /** Its name on the patch; null for an empty slot or a favorite the patch lacks. */
  readonly name: string | null;
  /** Browser-ready URL of its image; null when the catalogue holds none. */
  readonly image: string | null;
}
