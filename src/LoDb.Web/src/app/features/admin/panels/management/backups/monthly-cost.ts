import { frenchCurrency } from './french-currency';

const CENT = 0.01;
const CENTS: Intl.NumberFormatOptions = { minimumFractionDigits: 2, maximumFractionDigits: 2 };

/**
 * What the backups are estimated to cost a month, to the cent: `0,05 €`. Less than a cent
 * reads `< 0,01 €`, so that a real cost never reads as nothing.
 */
export function monthlyCost(amount: number, currency: string): string {
  return amount > 0 && amount < CENT
    ? `< ${frenchCurrency(CENT, currency, CENTS)}`
    : frenchCurrency(amount, currency, CENTS);
}
