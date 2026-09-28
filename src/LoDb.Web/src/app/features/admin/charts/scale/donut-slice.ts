/** A part of a whole, drawn as an arc of a donut. */
export interface DonutSlice {
  readonly name: string;
  readonly value: number;
  /** A CSS colour, a token: `var(--color-hex)`. */
  readonly color: string;
}
