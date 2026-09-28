/** A known value of a choice facet, with the label it is shown under. */
export interface FacetOption {
  /** Locale-independent token, written to the URL. */
  readonly value: string;
  /** Translated label. */
  readonly label: string;
}
