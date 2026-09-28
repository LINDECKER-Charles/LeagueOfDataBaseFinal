// The codes the login page translates for a Google sign-in (`login-error-key.ts`).
const GOOGLE_UNAVAILABLE = 'google-unavailable';
const GOOGLE_CANCELLED = 'google-cancelled';
const GOOGLE_FAILED = 'google-failed';

/**
 * The host's own failure codes (`GoogleFailures` of `LoDb.Desktop`), as the login page words
 * them. The API's codes (`account-banned`, `google-email-unverified`…) pass unchanged.
 */
const HOST_FAILURES: Readonly<Record<string, string>> = {
  'google-unavailable': GOOGLE_UNAVAILABLE,
  cancelled: GOOGLE_CANCELLED,
  'browser-unavailable': GOOGLE_FAILED,
  'google-denied': GOOGLE_FAILED,
  expired: GOOGLE_FAILED,
  'invalid-state': GOOGLE_FAILED,
  'google-exchange-failed': GOOGLE_FAILED,
};

/** The `?error=` code of the login page for a failure of the host's Google sign-in. */
export function loginErrorCode(failure: string): string {
  return HOST_FAILURES[failure] ?? failure;
}
