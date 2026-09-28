// What each refusal of a sign-in says; keys of the `admin` scope.
const LOGIN_ERRORS: Readonly<Record<string, string>> = {
  'invalid-credentials': 'login.errors.invalid_credentials',
  'invalid-two-factor-code': 'login.errors.invalid_code',
  'account-banned': 'login.errors.banned',
  'account-locked': 'login.errors.locked',
  'rate-limited': 'login.errors.rate_limited',
};

/** The message of a refused sign-in, a key of the `admin` scope; the generic one otherwise. */
export function adminLoginErrorKey(code: string | null): string {
  return LOGIN_ERRORS[code ?? ''] ?? 'login.errors.generic';
}
