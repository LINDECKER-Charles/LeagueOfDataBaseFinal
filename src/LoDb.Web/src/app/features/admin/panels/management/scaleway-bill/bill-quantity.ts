// Intl spaces its thousands with narrow no-break spaces; the admin writes plain ones.
const SPACES = /\s/gu;
// Scaleway bills storage in whole gigabyte-hours, egress to the megabyte.
const QUANTITY = new Intl.NumberFormat('fr-FR', { maximumFractionDigits: 3 });

/** A quantity Scaleway billed, in French, negative for a deduction: `1 234,5`, `-1`. */
export function billQuantity(quantity: number): string {
  return QUANTITY.format(quantity || 0).replace(SPACES, ' ');
}
