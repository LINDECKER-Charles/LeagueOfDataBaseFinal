/** A text of the `admin` scope and the values it interpolates. */
export interface AdminMessage {
  readonly key: string;
  readonly params?: Readonly<Record<string, unknown>>;
}
