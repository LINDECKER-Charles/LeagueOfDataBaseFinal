import { qrCode } from './qr-code';
import type { QrModules } from './qr-matrix';
import { qrPenalty } from './qr-penalty';
import { reedSolomon } from './reed-solomon';

const URI = 'otpauth://totp/LODB:root%40example.com?secret=JBSWY3DPEHPK3PXP&issuer=LODB';
// The symbol of URI at level M with mask 5, rows in hexadecimal, as the reference encoder
// `qrcode` 1.5 draws it (a byte segment forced, `maskPattern: 5`).
const URI_MASK_5 = [
  '1fc85bc17f',
  '1052d14541',
  '175ca0c05d',
  '17589dd35d',
  '174afff35d',
  '1045e85941',
  '1fd555557f',
  '0019ed4700',
  '105b5244ce',
  '0d929c289e',
  '0e42faceab',
  '15ab473491',
  '0a41f16be1',
  '1da7f76202',
  '07ff74025b',
  '0d8241658f',
  '1ce12427ef',
  '03ab90656e',
  '126cd7eb05',
  '1918f230aa',
  '15e54955fa',
  '16196eee94',
  '0868b9c2c3',
  '0810ed6158',
  '0a44698b53',
  '1d174d25e4',
  '147378cbff',
  '108e10145c',
  '164db87bf8',
  '001377e71a',
  '1fc5aebd55',
  '104530331a',
  '174c44dbf5',
  '174908eec5',
  '174901992f',
  '104fb32049',
  '1fde4521d5',
];
// The version information of version 7, BCH (18, 6) encoded (ISO/IEC 18004 table D.1).
const VERSION_7 = 0b000111110010010100;
const VERSION_BITS = 18;

function hexRows(modules: QrModules): string[] {
  return modules.map((row) => {
    const bits = row.map((dark) => (dark ? '1' : '0')).join('');
    return BigInt(`0b${bits}`)
      .toString(16)
      .padStart(Math.ceil(row.length / 4), '0');
  });
}

describe('reedSolomon', () => {
  it('computes the error correction of a version 1-M block', () => {
    const data = [32, 91, 11, 120, 209, 114, 220, 77, 67, 64, 236, 17, 236, 17, 236, 17];

    expect(reedSolomon(data, 10)).toEqual([196, 35, 39, 119, 235, 215, 231, 226, 93, 23]);
  });
});

describe('qrCode', () => {
  it('draws the symbol the reference encoder draws', () => {
    expect(hexRows(qrCode(URI, 5)!)).toEqual(URI_MASK_5);
  });

  it.each([
    [14, 21],
    [15, 25],
    [213, 57],
    [214, 61],
    [666, 97],
  ])('fits %s bytes in the smallest version: %s modules a side', (length, side) => {
    expect(qrCode('a'.repeat(length))).toHaveLength(side);
  });

  it('counts the bytes of the UTF-8 encoding, not the characters', () => {
    expect(qrCode('é'.repeat(7))).toHaveLength(21);
    expect(qrCode('é'.repeat(8))).toHaveLength(25);
  });

  it('gives up past version 20', () => {
    expect(qrCode('a'.repeat(667))).toBeNull();
  });

  it('writes the version information from version 7, in both copies', () => {
    const modules = qrCode('a'.repeat(110))!;
    const side = modules.length;
    let lowerLeft = 0;
    let upperRight = 0;
    for (let i = VERSION_BITS - 1; i >= 0; i--) {
      const [a, b] = [side - 11 + (i % 3), Math.floor(i / 3)];
      lowerLeft = (lowerLeft << 1) | (modules[a]?.[b] ? 1 : 0);
      upperRight = (upperRight << 1) | (modules[b]?.[a] ? 1 : 0);
    }

    expect(side).toBe(45);
    expect(lowerLeft).toBe(VERSION_7);
    expect(upperRight).toBe(VERSION_7);
  });

  it('keeps the mask with the lowest penalty', () => {
    const chosen = qrCode(URI)!;
    const penalties = [0, 1, 2, 3, 4, 5, 6, 7].map((mask) => qrPenalty(qrCode(URI, mask)!));

    expect(penalties).toContain(qrPenalty(chosen));
    expect(qrPenalty(chosen)).toBe(Math.min(...penalties));
  });

  it('frames the symbol with its three finders', () => {
    const modules = qrCode(URI)!;
    const last = modules.length - 1;
    const finderRow = [true, true, true, true, true, true, true, false];

    expect(modules[0]?.slice(0, 8)).toEqual(finderRow);
    expect(modules[0]?.slice(last - 7).reverse()).toEqual(finderRow);
    expect(modules[last]?.slice(0, 8)).toEqual(finderRow);
  });
});
