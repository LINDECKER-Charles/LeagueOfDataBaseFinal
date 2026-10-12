// Intl spaces its thousands and its symbol with narrow no-break spaces; the admin writes
// plain ones everywhere (format/figure.ts).
const SPACES = /\s/gu;

/** An amount in `currency`, written in French with `options`: `0,05 €`. */
export function frenchCurrency(
  amount: number,
  currency: string,
  options: Intl.NumberFormatOptions,
): string {
  return new Intl.NumberFormat('fr-FR', { style: 'currency', currency, ...options })
    .format(amount)
    .replace(SPACES, ' ');
}
