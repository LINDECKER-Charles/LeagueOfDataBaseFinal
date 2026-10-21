// The bit of each type of page in a mask of types held: the order of the API's ResourceType.
const TYPE_BITS: Readonly<Record<string, number>> = {
  champions: 0b0001,
  items: 0b0010,
  runes: 0b0100,
  summoners: 0b1000,
};
const EVERY_TYPE = 0b1111;
const HEXADECIMAL = 16;

// The mask of the types asked; a type the admin does not know can never be held.
function maskOf(types: readonly string[] | null): number | null {
  if (types === null) {
    return EVERY_TYPE;
  }
  let mask = 0;
  for (const type of types) {
    const bit = TYPE_BITS[type];
    if (bit === undefined) {
      return null;
    }
    mask |= bit;
  }
  return mask;
}

/**
 * Whether the (version, locale) holds every type asked: its mask includes theirs (§ 3.4).
 * `mask` is the hexadecimal character of the coverage (`f` holds all four, `0` none);
 * `types` null asks for all four.
 */
export function isHeld(mask: string, types: readonly string[] | null): boolean {
  const held = Number.parseInt(mask, HEXADECIMAL);
  const asked = maskOf(types);
  if (Number.isNaN(held) || asked === null) {
    return false;
  }
  return (held & asked) === asked;
}
