import { CSP_REPORTING } from './csp-reporting';

// The policies the pages create: Angular's own (its sanitizer, and the bypass the Riot rich
// text goes through) and the site's worker registration (src/sw/registration).
const POLICY_NAMES = ['angular', 'angular#unsafe-bypass', 'lodb#worker'];

/**
 * The Trusted Types policy of a page, sent as `Content-Security-Policy-Report-Only`: the
 * browser reports every string that reaches a script or HTML sink outside these policies, and
 * blocks nothing yet. Once the reports stay empty, it moves into the enforced policy.
 */
export function trustedTypesPolicy(): string {
  return [
    "require-trusted-types-for 'script'",
    ['trusted-types', ...POLICY_NAMES].join(' '),
    ...CSP_REPORTING.directives,
  ].join('; ');
}
