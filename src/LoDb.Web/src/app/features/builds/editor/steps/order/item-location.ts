/** A place in the purchase order: the index of a step, and a position among its items. */
export interface ItemLocation {
  readonly step: number;
  readonly index: number;
}
