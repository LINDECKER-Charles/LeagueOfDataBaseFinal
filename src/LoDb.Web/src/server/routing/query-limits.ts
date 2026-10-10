/** Bounds of the query a list or a detail of the catalogue keeps (`preRouteOf`). */
export interface QueryLimits {
  /** Parameters kept, the first ones; the others are dropped by the cleaning redirect. */
  readonly maxParams: number;
  /** Length of one `name=value` kept, as the URL writes it. */
  readonly maxParamLength: number;
}
