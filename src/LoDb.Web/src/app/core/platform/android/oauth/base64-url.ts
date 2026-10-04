/**
 * Base64url without padding (RFC 4648, section 5), the encoding of PKCE. `btoa` rather than
 * `Uint8Array.prototype.toBase64`, which older Android WebViews lack.
 */
export function base64Url(bytes: Uint8Array): string {
  const binary = Array.from(bytes, (byte) => String.fromCharCode(byte)).join('');
  return btoa(binary).replace(/\+/g, '-').replace(/\//g, '_').replace(/=+$/, '');
}
