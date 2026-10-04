const ROW = /^row(\d+)$/;

/** The number of a minor row (`row2` is 2); null for the keystones. */
export function rowNumberOf(slot: string): number | null {
  const digits = ROW.exec(slot)?.[1];
  return digits === undefined ? null : Number(digits);
}
