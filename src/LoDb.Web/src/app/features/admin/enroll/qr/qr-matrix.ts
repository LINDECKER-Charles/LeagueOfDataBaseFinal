import { QR_LEVEL_M } from './qr-level-m';

/** The modules of a symbol, row by row, `true` for a dark one. */
export type QrModules = readonly (readonly boolean[])[];

type MaskRule = (x: number, y: number) => boolean;

// The eight data masks of ISO/IEC 18004 table 10: a module whose rule holds is inverted.
const MASKS: readonly MaskRule[] = [
  (x, y) => (x + y) % 2 === 0,
  (_x, y) => y % 2 === 0,
  (x) => x % 3 === 0,
  (x, y) => (x + y) % 3 === 0,
  (x, y) => (Math.floor(x / 3) + Math.floor(y / 2)) % 2 === 0,
  (x, y) => ((x * y) % 2) + ((x * y) % 3) === 0,
  (x, y) => (((x * y) % 2) + ((x * y) % 3)) % 2 === 0,
  (x, y) => (((x + y) % 2) + ((x * y) % 3)) % 2 === 0,
];
const SIDE_STEP = 4;
const SIDE_BASE = 17;
// The timing patterns run along row and column 6.
const TIMING = 6;
// The rings of a finder from its centre out, dark or light: a 3 × 3 core, a light ring, a
// dark ring, then the light separator; an alignment pattern is a dot in a dark ring.
const FINDER_RINGS = [true, true, false, true, false] as const;
const ALIGNMENT_RINGS = [true, false, true] as const;
const FINDER_CENTRE = 3;
const ALIGNMENT_STEP = 7;
const VERSION_INFO_FROM = 7;
// BCH codes of the format (15, 5) and version (18, 6) information, and the format's mask.
const FORMAT_POLYNOMIAL = 0x537;
const FORMAT_MASK = 0x5412;
const FORMAT_ECC_BITS = 10;
const FORMAT_BITS = 15;
const MASK_BITS = 3;
const VERSION_POLYNOMIAL = 0x1f25;
const VERSION_ECC_BITS = 12;
const VERSION_BITS = 18;
const VERSION_BLOCK = 3;
const VERSION_OFFSET = 11;
const BYTE_BITS = 8;
// The format information runs along row and column 8, beside the finders.
const FORMAT_EDGE = 8;

function bitOf(value: number, index: number): boolean {
  return ((value >>> index) & 1) !== 0;
}

// The remainder of `data`, shifted by `degree`, divided by `polynomial` in GF(2).
function bch(data: number, polynomial: number, degree: number): number {
  let remainder = data;
  for (let i = 0; i < degree; i++) {
    remainder = (remainder << 1) ^ ((remainder >>> (degree - 1)) * polynomial);
  }
  return (data << degree) | remainder;
}

// The first copy of format bit `i`: down the column beside the upper-left finder, over the
// timing row, then leftwards along the row under it, over the timing column.
function firstFormatModule(i: number): readonly [number, number] {
  if (i < TIMING) {
    return [FORMAT_EDGE, i];
  }
  if (i < FORMAT_EDGE) {
    return [FORMAT_EDGE, i + 1];
  }
  return [FORMAT_BITS - i - (i === FORMAT_EDGE ? 0 : 1), FORMAT_EDGE];
}

/**
 * The module grid of a symbol of one version: its function patterns (finders, timing,
 * alignments, format and version information) drawn and reserved, then the data placed in
 * the remaining modules and masked (ISO/IEC 18004 §7.7 to §7.9).
 */
export class QrMatrix {
  readonly size: number;
  private readonly dark: boolean[][];
  private readonly reserved: boolean[][];

  constructor(private readonly version: number) {
    this.size = version * SIDE_STEP + SIDE_BASE;
    this.dark = Array.from({ length: this.size }, () => new Array<boolean>(this.size).fill(false));
    this.reserved = this.dark.map((row) => [...row]);
    this.drawTiming();
    this.drawFinders();
    this.drawAlignments();
    this.drawFormat(0);
    this.drawVersion();
  }

  /** A copy of the modules as they stand. */
  modules(): QrModules {
    return this.dark.map((row) => [...row]);
  }

  /** Places the codewords, most significant bit first, in the two-column zigzag. */
  place(codewords: readonly number[]): void {
    const bits = codewords.length * BYTE_BITS;
    let index = 0;
    for (const [x, y] of this.zigzag()) {
      if (!this.reserved[y]?.[x] && index < bits) {
        this.setData(x, y, bitOf(codewords[index >>> 3] ?? 0, BYTE_BITS - 1 - (index & 7)));
        index++;
      }
    }
  }

  /** Inverts the data under mask `mask` and writes its format information; twice undoes. */
  toggleMask(mask: number): void {
    const rule = MASKS[mask] ?? MASKS[0];
    for (let y = 0; y < this.size; y++) {
      for (let x = 0; x < this.size; x++) {
        if (!this.reserved[y]?.[x] && rule?.(x, y)) {
          this.setData(x, y, !this.dark[y]?.[x]);
        }
      }
    }
    this.drawFormat(mask);
  }

  private *zigzag(): Generator<readonly [number, number]> {
    for (let right = this.size - 1; right >= 1; right -= 2) {
      // The columns step over the vertical timing pattern.
      const column = right === TIMING ? right - 1 : right;
      const upward = ((column + 1) & 2) === 0;
      for (let step = 0; step < this.size; step++) {
        const y = upward ? this.size - 1 - step : step;
        yield [column, y];
        yield [column - 1, y];
      }
      right = column;
    }
  }

  private setData(x: number, y: number, dark: boolean): void {
    const row = this.dark[y];
    if (row) {
      row[x] = dark;
    }
  }

  private set(x: number, y: number, dark: boolean): void {
    this.setData(x, y, dark);
    const row = this.reserved[y];
    if (row) {
      row[x] = true;
    }
  }

  private drawTiming(): void {
    for (let i = 0; i < this.size; i++) {
      this.set(TIMING, i, i % 2 === 0);
      this.set(i, TIMING, i % 2 === 0);
    }
  }

  private drawFinders(): void {
    const far = this.size - 1 - FINDER_CENTRE;
    for (const [cx, cy] of [
      [FINDER_CENTRE, FINDER_CENTRE],
      [far, FINDER_CENTRE],
      [FINDER_CENTRE, far],
    ] as const) {
      this.drawRings(cx, cy, FINDER_RINGS);
    }
  }

  // Square rings around a centre; the separators of the finders fall off the edges.
  private drawRings(cx: number, cy: number, rings: readonly boolean[]): void {
    const reach = rings.length - 1;
    for (let dy = -reach; dy <= reach; dy++) {
      for (let dx = -reach; dx <= reach; dx++) {
        const [x, y] = [cx + dx, cy + dy];
        if (x >= 0 && x < this.size && y >= 0 && y < this.size) {
          this.set(x, y, rings[Math.max(Math.abs(dx), Math.abs(dy))] ?? false);
        }
      }
    }
  }

  private drawAlignments(): void {
    const positions = this.alignmentPositions();
    const last = positions.length - 1;
    positions.forEach((y, row) =>
      positions.forEach((x, column) => {
        // The three corners of the finders keep no alignment pattern.
        const corner =
          (row === 0 && column === 0) ||
          (row === 0 && column === last) ||
          (row === last && column === 0);
        if (!corner) {
          this.drawRings(x, y, ALIGNMENT_RINGS);
        }
      }),
    );
  }

  private alignmentPositions(): number[] {
    if (this.version === 1) {
      return [];
    }
    const count = Math.floor(this.version / ALIGNMENT_STEP) + 2;
    const step = Math.ceil((this.version * 4 + 4) / (count * 2 - 2)) * 2;
    const positions = [TIMING];
    for (let position = this.size - ALIGNMENT_STEP; positions.length < count; position -= step) {
      positions.splice(1, 0, position);
    }
    return positions;
  }

  // Both copies of the 15 format bits, and the dark module beside the lower-left finder.
  private drawFormat(mask: number): void {
    const data = (QR_LEVEL_M.formatBits << MASK_BITS) | mask;
    const bits = bch(data, FORMAT_POLYNOMIAL, FORMAT_ECC_BITS) ^ FORMAT_MASK;
    const edge = FORMAT_EDGE;
    for (let i = 0; i < FORMAT_BITS; i++) {
      const dark = bitOf(bits, i);
      const [x, y] = firstFormatModule(i);
      this.set(x, y, dark);
      // Right to left under the upper-right finder, then up beside the lower-left one.
      if (i < edge) {
        this.set(this.size - 1 - i, edge, dark);
      } else {
        this.set(edge, this.size - FORMAT_BITS + i, dark);
      }
    }
    this.set(edge, this.size - edge, true);
  }

  private drawVersion(): void {
    if (this.version < VERSION_INFO_FROM) {
      return;
    }
    const bits = bch(this.version, VERSION_POLYNOMIAL, VERSION_ECC_BITS);
    for (let i = 0; i < VERSION_BITS; i++) {
      const a = this.size - VERSION_OFFSET + (i % VERSION_BLOCK);
      const b = Math.floor(i / VERSION_BLOCK);
      this.set(a, b, bitOf(bits, i));
      this.set(b, a, bitOf(bits, i));
    }
  }
}
