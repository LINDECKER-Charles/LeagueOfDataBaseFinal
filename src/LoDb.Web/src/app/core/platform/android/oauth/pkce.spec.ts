import { pkceChallenge } from './pkce-challenge';
import { pkceSecret } from './pkce-secret';

describe('PKCE', () => {
  it('draws 43-character URL-safe secrets, a new one each time', () => {
    const first = pkceSecret();

    expect(first).toMatch(/^[A-Za-z0-9_-]{43}$/);
    expect(pkceSecret()).not.toBe(first);
  });

  it('derives the S256 challenge, base64url without padding', async () => {
    // Computed independently with Node's crypto: sha256(verifier) in base64url.
    const verifier = 'dBjftJeZ4CVP-mJ92ZAcTWnDqsXtdJ7bWxyOrwpkHZpDE3Wd9Bo';

    await expect(pkceChallenge(verifier)).resolves.toBe(
      'JqXUhczNyeAUdKynvmgfOW-dL0yi5xG8xXIDR1roSEY',
    );
  });
});
