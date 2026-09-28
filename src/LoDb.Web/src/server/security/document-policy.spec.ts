import { documentPolicy } from './document-policy';

function directives(policy: string): Map<string, string> {
  return new Map(
    policy.split('; ').map((directive) => {
      const [name, ...sources] = directive.split(' ');
      return [name, sources.join(' ')];
    }),
  );
}

describe('documentPolicy', () => {
  it('admits the site scripts and exactly the hashed inline ones', () => {
    const policy = directives(documentPolicy(["'sha256-a'", "'sha256-b'"]));

    expect(policy.get('script-src')).toBe("'self' 'unsafe-eval' 'sha256-a' 'sha256-b'");
  });

  it('admits no inline script when there is none', () => {
    expect(directives(documentPolicy([])).get('script-src')).toBe("'self' 'unsafe-eval'");
  });

  it('writes the policy of ADR 0005', () => {
    const policy = directives(documentPolicy([]));

    expect(Object.fromEntries(policy)).toEqual({
      'default-src': "'self'",
      'base-uri': "'self'",
      'object-src': "'none'",
      'frame-src': "'none'",
      'frame-ancestors': "'none'",
      'form-action': "'self' https://checkout.stripe.com https://billing.stripe.com",
      'script-src': "'self' 'unsafe-eval'",
      'style-src': "'self' 'unsafe-inline'",
      'img-src':
        "'self' data: https://ddragon.leagueoflegends.com https://raw.communitydragon.org https://d28xe8vt774jo5.cloudfront.net",
      'media-src':
        "'self' https://ddragon.leagueoflegends.com https://raw.communitydragon.org https://d28xe8vt774jo5.cloudfront.net",
      'font-src': "'self'",
      'connect-src': "'self'",
      'worker-src': "'self'",
      'manifest-src': "'self'",
    });
  });

  it('lets no inline code in by a keyword', () => {
    expect(documentPolicy(["'sha256-a'"])).not.toMatch(
      /strict-dynamic|unsafe-hashes|script-src[^;]*unsafe-inline/,
    );
  });
});
