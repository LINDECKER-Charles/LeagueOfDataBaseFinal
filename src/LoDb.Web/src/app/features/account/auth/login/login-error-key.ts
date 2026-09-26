// What each refusal of a sign-in says, the Google ones (`?error=` of the callback) included.
const LOGIN_ERRORS: Readonly<Record<string, string>> = {
  'invalid-credentials': 'account.errors.invalid_credentials',
  'invalid-two-factor-code': 'account.errors.invalid_two_factor',
  'account-banned': 'auth.flash.banned',
  'account-locked': 'account.errors.locked',
  'rate-limited': 'auth.flash.too_many_attempts',
  'google-unavailable': 'auth.google.unavailable',
  'google-cancelled': 'account.errors.google_cancelled',
  'google-failed': 'auth.google.failed',
  'google-email-unverified': 'account.errors.google_email_unverified',
  // Only Google answers it as an error: a password sign-in asks for the code instead.
  'two-factor-required': 'account.errors.google_two_factor',
};

/** The message of a refused sign-in, a translation key; the generic one for anything else. */
export function loginErrorKey(code: string | null): string {
  return LOGIN_ERRORS[code ?? ''] ?? 'account.errors.generic';
}
