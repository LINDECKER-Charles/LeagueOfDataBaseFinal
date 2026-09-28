const THOUSANDS = /\B(?=(\d{3})+(?!\d))/g;

/**
 * A request count grouped by thousands with a space, in every locale: the legacy price list
 * (`number_format(0, '.', ' ')`) never followed the locale's own grouping.
 */
export function groupThousands(count: number): string {
  return String(count).replace(THOUSANDS, ' ');
}
