/** A fix listed by a release, with its tracker id and area when the file names them. */
export interface ReleaseFix {
  readonly id: string | null;
  readonly area: string | null;
  readonly text: string;
}
