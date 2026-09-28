/**
 * The error correction level M of ISO/IEC 18004 (table 9), up to version 20: about 15 %
 * of the symbol may be lost, which a phone screen or a print both survive. An
 * `otpauth://` URI fits in version 10 at most.
 */
export const QR_LEVEL_M = {
  /** The last version the encoder supports: 97 × 97 modules. */
  maxVersion: 20,
  /** The error correction codewords of each block, by version (index 0 unused). */
  eccPerBlock: [0, 10, 16, 26, 18, 24, 16, 18, 22, 22, 26, 30, 22, 22, 24, 24, 28, 28, 26, 26, 26],
  /** The number of error correction blocks, by version (index 0 unused). */
  blocks: [0, 1, 1, 1, 2, 2, 4, 4, 4, 5, 5, 5, 8, 9, 9, 10, 10, 11, 13, 14, 16],
  /** The two bits of the level in the format information. */
  formatBits: 0b00,
} as const;
