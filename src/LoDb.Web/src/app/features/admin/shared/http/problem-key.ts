import type { AdminProblem } from './problem-of';

// The refusals the admin explains, by the API's code; keys of the `admin` scope.
const PROBLEM_KEYS: Readonly<Record<string, string>> = {
  'self-moderation': 'errors.self_moderation',
  'api-client-revoked': 'errors.api_client_revoked',
  'user-not-found': 'errors.gone',
  'build-not-found': 'errors.gone',
  'contact-not-found': 'errors.gone',
  'api-client-not-found': 'errors.gone',
  'authentication-required': 'errors.session',
  'mfa-required': 'errors.session',
  forbidden: 'errors.session',
  'rate-limited': 'errors.rate_limited',
  'validation-failed': 'errors.invalid',
};
// A session expired or opened without the second factor, whatever the code says.
const SESSION_STATUSES: readonly number[] = [401, 403];
// How HttpClient reports a request that got no answer.
const NO_RESPONSE = 0;

/** What the admin says of a failed call: a key of the `admin` scope. */
export function problemKey(problem: AdminProblem): string {
  const known = PROBLEM_KEYS[problem.code ?? ''];
  if (known) {
    return known;
  }
  if (SESSION_STATUSES.includes(problem.status)) {
    return 'errors.session';
  }
  return problem.status === NO_RESPONSE ? 'errors.network' : 'errors.generic';
}
