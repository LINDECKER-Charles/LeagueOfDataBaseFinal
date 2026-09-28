/** The skin banner as the editor holds it: the id it saves, its name and its art. */
export interface SkinChoice {
  /** The id saved, `{championId}_{number}` such as `Ahri_7`. */
  readonly id: string;
  readonly name: string;
  /** The centered splash the banner shows. */
  readonly banner: string;
}
