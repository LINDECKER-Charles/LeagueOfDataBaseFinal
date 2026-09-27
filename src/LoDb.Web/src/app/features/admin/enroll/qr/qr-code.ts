import { qrCodewords } from './qr-codewords';
import { QrMatrix, type QrModules } from './qr-matrix';
import { qrPenalty } from './qr-penalty';

const MASKS = [0, 1, 2, 3, 4, 5, 6, 7] as const;

// The mask whose symbol reads best; each trial undone before the next.
function bestMask(matrix: QrMatrix): number {
  const penalties = MASKS.map((mask) => {
    matrix.toggleMask(mask);
    const penalty = qrPenalty(matrix.modules());
    matrix.toggleMask(mask);
    return penalty;
  });
  return penalties.indexOf(Math.min(...penalties));
}

/**
 * The modules of the QR code of `text`, UTF-8 in one byte segment at level M, in the
 * smallest version that holds it and with the best mask unless `mask` forces one; null for a
 * text longer than version 20 holds. Enough for an `otpauth://` URI, without a library.
 */
export function qrCode(text: string, mask?: number): QrModules | null {
  const encoded = qrCodewords(new TextEncoder().encode(text));
  if (encoded === null) {
    return null;
  }
  const matrix = new QrMatrix(encoded.version);
  matrix.place(encoded.codewords);
  matrix.toggleMask(mask ?? bestMask(matrix));
  return matrix.modules();
}
