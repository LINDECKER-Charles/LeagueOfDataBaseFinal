/** One step of the purchase order as the editor holds it: never changed, always replaced. */
export interface OrderStep {
  readonly label: string;
  /** Null when the step has none. */
  readonly note: string | null;
  /** Item ids in purchase order; the same id may repeat. */
  readonly items: readonly string[];
}
