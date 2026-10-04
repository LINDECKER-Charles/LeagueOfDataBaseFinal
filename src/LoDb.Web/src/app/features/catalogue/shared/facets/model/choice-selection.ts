/** What a reader picked on a choice facet. */
export interface ChoiceSelection {
  readonly values: readonly string[];
  /** Every selected value must be carried by the card, instead of any of them. */
  readonly all: boolean;
}
