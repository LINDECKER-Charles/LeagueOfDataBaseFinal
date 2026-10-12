import { frenchCurrency } from './french-currency';

// Storage is priced in thousandths of a cent per GB: 0,01606 €.
const PRICE_DECIMALS = 5;

/** A price per GB and month, to its last significant decimal: `0,01606 €`. */
export function unitPrice(amount: number, currency: string): string {
  return frenchCurrency(amount, currency, { maximumFractionDigits: PRICE_DECIMALS });
}
