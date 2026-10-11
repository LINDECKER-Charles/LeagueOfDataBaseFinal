/**
 * The locale rules a compiled message needs: plural categories and number formatting, from
 * the runtime's `Intl`, built on first use and kept for the locale's lifetime (one per active
 * language of the application). A message that never pluralises builds none.
 */
export class LocaleFormats {
  private cardinalRules: Intl.PluralRules | null = null;
  private ordinalRules: Intl.PluralRules | null = null;
  private numberFormat: Intl.NumberFormat | null = null;

  constructor(readonly locale: string) {}

  /** The cardinal plural category of a count: `one`, `few`, `other`… */
  cardinal(value: number): string {
    this.cardinalRules ??= new Intl.PluralRules(this.locale);
    return this.cardinalRules.select(value);
  }

  /** The ordinal plural category of a rank (`selectordinal`). */
  ordinal(value: number): string {
    this.ordinalRules ??= new Intl.PluralRules(this.locale, { type: 'ordinal' });
    return this.ordinalRules.select(value);
  }

  /** A number as the locale writes it: what `#` shows inside a plural. */
  number(value: number): string {
    this.numberFormat ??= new Intl.NumberFormat(this.locale);
    return this.numberFormat.format(value);
  }
}
