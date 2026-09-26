/**
 * What the URL of the trends asks for: `?champion=&mode=&language=&page=`. A blank filter is
 * no filter; the API ignores a mode or a language it does not know, as the legacy page did.
 */
export interface TrendsQuery {
  /** A champion id, such as MonkeyKing. */
  readonly champion: string | null;
  /** A mode code, such as aram. */
  readonly mode: string | null;
  /** The Data Dragon language the builds are written in, such as fr_FR. */
  readonly language: string | null;
  /** From 1. */
  readonly page: number;
}
