// `yyyy-MM`, as the API names a billing period.
const PERIOD = /^(\d{4})-(\d{2})$/u;
const MONTH = new Intl.DateTimeFormat('fr-FR', { month: 'long', year: 'numeric', timeZone: 'UTC' });

/** A billing period (`2026-10`) as the admin reads it, in French: `octobre 2026`. */
export function billMonth(period: string): string {
  const match = PERIOD.exec(period);
  return match ? MONTH.format(Date.UTC(Number(match[1]), Number(match[2]) - 1)) : period;
}
