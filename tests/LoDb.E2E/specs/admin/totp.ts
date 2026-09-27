import { createHmac } from 'node:crypto';

// RFC 6238 as authenticator apps and ASP.NET Identity apply it: HMAC-SHA1, a code of six
// digits, a new one every 30 seconds.
const STEP_SECONDS = 30;
const DIGITS = 6;
const COUNTER_BYTES = 8;
const BASE32 = 'ABCDEFGHIJKLMNOPQRSTUVWXYZ234567';
const BITS_PER_CHARACTER = 5;
const BITS_PER_BYTE = 8;
const BYTE_MASK = 0xff;
// RFC 4226, dynamic truncation: the low nibble of the last byte picks four bytes, whose
// first bit is dropped.
const OFFSET_MASK = 0x0f;
const SIGN_MASK = 0x7f;

/** The time step `at` falls in: two codes of one step are the same code. */
export function totpStep(at: number = Date.now()): number {
  return Math.floor(at / 1000 / STEP_SECONDS);
}

/** When the step after `step` starts, in milliseconds. */
export function nextStepAt(step: number): number {
  return (step + 1) * STEP_SECONDS * 1000;
}

/** The code an authenticator app shows during `step` for the base32 `key`. */
export function totpCode(key: string, step: number = totpStep()): string {
  const counter = Buffer.alloc(COUNTER_BYTES);
  counter.writeBigUInt64BE(BigInt(step));
  const digest = createHmac('sha1', base32Bytes(key)).update(counter).digest();
  const offset = (digest.at(-1) ?? 0) & OFFSET_MASK;
  const bytes = [...digest.subarray(offset, offset + 4)];
  const [high = 0, ...low] = bytes;
  const value = low.reduce((sum, byte) => sum * 256 + byte, high & SIGN_MASK);
  return String(value % 10 ** DIGITS).padStart(DIGITS, '0');
}

// The key as the enrolment page shows it: grouped by spaces, in any case, maybe padded.
function base32Bytes(key: string): Buffer {
  const characters = key.toUpperCase().replace(/[\s=]/g, '');
  const bytes: number[] = [];
  let buffer = 0;
  let bits = 0;
  for (const character of characters) {
    const value = BASE32.indexOf(character);
    if (value < 0) {
      throw new Error(`"${character}" is no base32 character of the shared key`);
    }
    buffer = (buffer << BITS_PER_CHARACTER) | value;
    bits += BITS_PER_CHARACTER;
    if (bits >= BITS_PER_BYTE) {
      bits -= BITS_PER_BYTE;
      bytes.push((buffer >> bits) & BYTE_MASK);
      // Only the bits not read yet are kept, so the buffer never outgrows 32 bits.
      buffer &= (1 << bits) - 1;
    }
  }
  return Buffer.from(bytes);
}
