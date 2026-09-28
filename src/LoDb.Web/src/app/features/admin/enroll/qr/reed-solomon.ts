// The primitive polynomial of the QR code's Galois field GF(2^8): x^8 + x^4 + x^3 + x^2 + 1.
const FIELD_POLYNOMIAL = 0x11d;
const HIGH_BIT = 7;
const GENERATOR = 0x02;
const divisors = new Map<number, readonly number[]>();

// Russian peasant multiplication in GF(2^8), reduced by the field polynomial.
function multiply(x: number, y: number): number {
  let product = 0;
  for (let bit = HIGH_BIT; bit >= 0; bit--) {
    product = (product << 1) ^ ((product >>> HIGH_BIT) * FIELD_POLYNOMIAL);
    product ^= ((y >>> bit) & 1) * x;
  }
  return product;
}

// The generator polynomial of `degree`: the product of (x - 2^i) for i below `degree`, its
// leading 1 left out; the highest coefficient first.
function divisorOf(degree: number): readonly number[] {
  const divisor = [...new Array<number>(degree - 1).fill(0), 1];
  let root = 1;
  for (let i = 0; i < degree; i++) {
    for (let j = 0; j < divisor.length; j++) {
      divisor[j] = multiply(divisor[j] ?? 0, root) ^ (divisor[j + 1] ?? 0);
    }
    root = multiply(root, GENERATOR);
  }
  return divisor;
}

/**
 * The `degree` error correction codewords of a block of `data` codewords: the remainder of
 * its division by the generator polynomial (ISO/IEC 18004 §7.5.2).
 */
export function reedSolomon(data: readonly number[], degree: number): number[] {
  let divisor = divisors.get(degree);
  if (!divisor) {
    divisor = divisorOf(degree);
    divisors.set(degree, divisor);
  }
  const remainder = new Array<number>(degree).fill(0);
  for (const codeword of data) {
    const factor = codeword ^ (remainder.shift() ?? 0);
    remainder.push(0);
    divisor.forEach((coefficient, i) => (remainder[i] ^= multiply(coefficient, factor)));
  }
  return remainder;
}
