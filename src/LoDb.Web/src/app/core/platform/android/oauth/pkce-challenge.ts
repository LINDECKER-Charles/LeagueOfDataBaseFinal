import { base64Url } from './base64-url';

/** The S256 challenge of a PKCE verifier (RFC 7636, section 4.2). */
export async function pkceChallenge(verifier: string): Promise<string> {
  const digest = await crypto.subtle.digest('SHA-256', new TextEncoder().encode(verifier));
  return base64Url(new Uint8Array(digest));
}
