/**
 * A stat as the board prints it: two decimals below 10 (an attack speed of 0.63), one below
 * 1000 (an armor of 32.5), none above (a health of 2081).
 */
export function formatStat(value: number): string {
  if (value < 10) {
    return String(Math.round(value * 100) / 100);
  }
  if (value < 1000) {
    return String(Math.round(value * 10) / 10);
  }
  return String(Math.round(value));
}
