import { base64Url } from './base64-url';

// 256 bits, 43 characters once encoded: the shortest verifier RFC 7636 accepts.
const SECRET_BYTES = 32;

/** A random URL-safe secret, used as a PKCE verifier or an OAuth state. */
export function pkceSecret(): string {
  return base64Url(crypto.getRandomValues(new Uint8Array(SECRET_BYTES)));
}
