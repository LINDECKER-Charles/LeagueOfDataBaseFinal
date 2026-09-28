/** One value of a choice facet as the console draws it. */
export interface ChoiceRow {
  readonly value: string;
  readonly label: string;
  readonly isOn: boolean;
  /** Result count in the current context. */
  readonly count: number;
  /** No card in context carries it and it is not picked: shown, but cannot be chosen. */
  readonly isDisabled: boolean;
}
