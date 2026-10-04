/** A router link of the home page: an absolute path and its query, `?lang=` for a variant. */
export interface HomeLink {
  readonly path: string;
  readonly query: Readonly<Record<string, string>>;
}
