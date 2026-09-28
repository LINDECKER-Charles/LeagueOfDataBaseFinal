/** One cast figure of an ability: cooldown, cost, range, charges or ranks. */
export interface AbilityChip {
  /** Translation key of the label. */
  readonly label: string;
  /** Per rank, as Data Dragon writes it: "7/6.5/6/5.5/5". */
  readonly value: string;
  /** Unit after the value: seconds, or the champion's resource; empty for none. */
  readonly unit: string;
}
