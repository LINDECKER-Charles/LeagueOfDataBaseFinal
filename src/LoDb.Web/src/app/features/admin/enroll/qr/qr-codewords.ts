import { QR_LEVEL_M } from './qr-level-m';
import { rawModules } from './raw-modules';
import { reedSolomon } from './reed-solomon';

/** The codewords of a symbol, blocks interleaved, and the version they fill. */
export interface QrCodewords {
  readonly version: number;
  readonly codewords: readonly number[];
}

const BYTE_BITS = 8;
// The mode indicator of 8-bit bytes (ISO/IEC 18004 table 2).
const BYTE_MODE = 0b0100;
const MODE_BITS = 4;
// The count of bytes takes 8 bits up to version 9, 16 from version 10 (table 3).
const LONG_COUNT_FROM = 10;
const SHORT_COUNT_BITS = 8;
const LONG_COUNT_BITS = 16;
const TERMINATOR_BITS = 4;
// The pad codewords alternate after the data (§7.4.10).
const PAD_CODEWORDS = [0xec, 0x11] as const;

function countBits(version: number): number {
  return version < LONG_COUNT_FROM ? SHORT_COUNT_BITS : LONG_COUNT_BITS;
}

function dataCapacity(version: number): number {
  const ecc = QR_LEVEL_M.eccPerBlock[version] ?? 0;
  const blocks = QR_LEVEL_M.blocks[version] ?? 0;
  return Math.floor(rawModules(version) / BYTE_BITS) - ecc * blocks;
}

// The smallest version whose data codewords hold the segment.
function versionFor(length: number): number | null {
  for (let version = 1; version <= QR_LEVEL_M.maxVersion; version++) {
    const needed = MODE_BITS + countBits(version) + length * BYTE_BITS;
    if (needed <= dataCapacity(version) * BYTE_BITS) {
      return version;
    }
  }
  return null;
}

function append(bits: number[], value: number, length: number): void {
  for (let bit = length - 1; bit >= 0; bit--) {
    bits.push((value >>> bit) & 1);
  }
}

// One byte segment, the terminator, the padding to a byte, then the pad codewords.
function dataCodewords(bytes: Uint8Array, version: number): number[] {
  const capacity = dataCapacity(version) * BYTE_BITS;
  const bits: number[] = [];
  append(bits, BYTE_MODE, MODE_BITS);
  append(bits, bytes.length, countBits(version));
  bytes.forEach((byte) => append(bits, byte, BYTE_BITS));
  append(bits, 0, Math.min(TERMINATOR_BITS, capacity - bits.length));
  append(bits, 0, (BYTE_BITS - (bits.length % BYTE_BITS)) % BYTE_BITS);
  for (let pad = 0; bits.length < capacity; pad++) {
    append(bits, PAD_CODEWORDS[pad % PAD_CODEWORDS.length] ?? 0, BYTE_BITS);
  }
  const codewords: number[] = [];
  for (let i = 0; i < bits.length; i += BYTE_BITS) {
    codewords.push(bits.slice(i, i + BYTE_BITS).reduce((byte, bit) => (byte << 1) | bit, 0));
  }
  return codewords;
}

// Splits the data into blocks, the short ones first, adds their error correction, then
// interleaves them codeword by codeword (§7.6).
function interleave(data: readonly number[], version: number): number[] {
  const blocks = QR_LEVEL_M.blocks[version] ?? 1;
  const ecc = QR_LEVEL_M.eccPerBlock[version] ?? 0;
  const raw = Math.floor(rawModules(version) / BYTE_BITS);
  const shortBlocks = blocks - (raw % blocks);
  const shortData = Math.floor(raw / blocks) - ecc;
  const dataBlocks: number[][] = [];
  for (let i = 0, start = 0; i < blocks; i++) {
    const end = start + shortData + (i < shortBlocks ? 0 : 1);
    dataBlocks.push(data.slice(start, end));
    start = end;
  }
  const longest = shortData + (shortBlocks < blocks ? 1 : 0);
  const result: number[] = [];
  for (let i = 0; i < longest; i++) {
    // The short blocks have no codeword at the last position of the data.
    for (const block of dataBlocks.filter((candidate) => i < candidate.length)) {
      result.push(block[i] ?? 0);
    }
  }
  const corrections = dataBlocks.map((block) => reedSolomon(block, ecc));
  for (let i = 0; i < ecc; i++) {
    corrections.forEach((block) => result.push(block[i] ?? 0));
  }
  return result;
}

/**
 * The codewords of `bytes` in one byte-mode segment at level M, in the smallest version
 * that holds them; null past version 20.
 */
export function qrCodewords(bytes: Uint8Array): QrCodewords | null {
  const version = versionFor(bytes.length);
  return version === null
    ? null
    : { version, codewords: interleave(dataCodewords(bytes, version), version) };
}
