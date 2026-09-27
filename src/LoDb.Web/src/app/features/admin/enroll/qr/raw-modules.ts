// The modules of the three finders, their separators, the format and the timing patterns,
// counted in the quadratic below (ISO/IEC 18004 §7.1, table 1).
const BASE = 64;
// Each alignment pattern takes 25 modules, those on the timing lines 5 fewer.
const ALIGNMENT_AREA = 25;
const ALIGNMENT_ON_TIMING = 10;
const ALIGNMENT_OVERLAP = 55;
// Two 6 × 3 blocks of version information from version 7.
const VERSION_INFO_FROM = 7;
const VERSION_INFO_MODULES = 36;
const ALIGNMENT_STEP = 7;
const ALIGNMENT_MIN = 2;
const SIDE_STEP = 16;
const SIDE_BASE = 128;

/**
 * The modules of a symbol of `version` that carry data and error correction, every
 * function pattern taken away: `(16v + 128)v + 64` less the alignments and the version
 * information.
 */
export function rawModules(version: number): number {
  let modules = (SIDE_STEP * version + SIDE_BASE) * version + BASE;
  if (version >= ALIGNMENT_MIN) {
    const alignments = Math.floor(version / ALIGNMENT_STEP) + ALIGNMENT_MIN;
    modules -= (ALIGNMENT_AREA * alignments - ALIGNMENT_ON_TIMING) * alignments - ALIGNMENT_OVERLAP;
    if (version >= VERSION_INFO_FROM) {
      modules -= VERSION_INFO_MODULES;
    }
  }
  return modules;
}
