import { frenchCurrency } from '../backups/french-currency';

// Scaleway bills to the ten-thousandth of a euro, and a new project's months cost a few
// cents: the bill keeps those decimals where the estimates round to the cent.
const AMOUNT: Intl.NumberFormatOptions = { minimumFractionDigits: 2, maximumFractionDigits: 4 };

/**
 * An amount of the Scaleway bill, excluding VAT, written in French down to its last
 * significant decimal: `0,0517 €`, `-0,01 €` for a deduction, `0,00 €` for nothing.
 */
export function billAmount(amount: number, currency: string): string {
  // A sum of opposite lines may come back as -0, which Intl writes with its sign.
  return frenchCurrency(amount || 0, currency, AMOUNT);
}
