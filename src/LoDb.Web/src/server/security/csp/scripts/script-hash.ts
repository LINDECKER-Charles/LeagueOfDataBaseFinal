import { createHash } from 'node:crypto';

/** The CSP source (`'sha256-…'`) that admits an inline script of exactly this text. */
export function scriptHash(text: string): string {
  return `'sha256-${createHash('sha256').update(text, 'utf8').digest('base64')}'`;
}
