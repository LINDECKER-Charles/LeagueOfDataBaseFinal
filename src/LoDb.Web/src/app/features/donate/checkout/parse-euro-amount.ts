// Up to three digits of euros, then up to two of cents after a point or a comma: the
// pattern of the legacy form, whatever the decimal separator of the donor's keyboard.
const EURO_AMOUNT = /^(?<euros>\d{1,3})(?:[.,](?<cents>\d{1,2}))?$/;

/**
 * The cents of a free amount typed in euros (`7`, `7.5`, `7,50`), or null when the text is
 * no such amount. The bounds of a donation are checked apart, against the API's options.
 */
export function parseEuroAmount(text: string): number | null {
  const groups = EURO_AMOUNT.exec(text.trim())?.groups;
  if (groups === undefined) {
    return null;
  }
  const cents = (groups['cents'] ?? '').padEnd(2, '0');
  return Number(groups['euros']) * 100 + Number(cents);
}
