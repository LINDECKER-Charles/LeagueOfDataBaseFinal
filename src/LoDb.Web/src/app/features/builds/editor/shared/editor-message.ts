/** A message the editor shows: a translation key and what it interpolates. */
export interface EditorMessage {
  readonly key: string;
  readonly params?: Readonly<Record<string, string>>;
}
