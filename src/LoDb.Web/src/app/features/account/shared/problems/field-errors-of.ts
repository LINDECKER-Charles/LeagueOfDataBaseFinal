import type { ApiProblem } from './api-problem';
import type { FieldErrors } from './field-errors';

// The message of each field code the API answers. The password codes are translation keys
// already (`auth.password.rule_digit`), all but the byte limit the checklist never shows.
const FIELD_MESSAGES: Readonly<Record<string, string>> = {
  required: 'account.errors.required',
  'email-invalid': 'account.errors.email_invalid',
  'email-too-long': 'account.errors.email_too_long',
  'email-taken': 'account.errors.email_taken',
  'username-invalid': 'profile.identity.username_invalid',
  'username-taken': 'profile.identity.username_taken',
  'terms-required': 'account.errors.terms_required',
  'tagline-invalid': 'profile.identity.tag_invalid',
  'password-incorrect': 'profile.flash.wrong_password',
  'confirmation-incorrect': 'profile.flash.wrong_phrase',
  'auth.password.too_long': 'account.errors.password_too_long',
};
const PASSWORD_CODE_PREFIX = 'auth.password.';
const UNKNOWN_CODE = 'account.errors.invalid';

function messageOf(code: string): string {
  return FIELD_MESSAGES[code] ?? (code.startsWith(PASSWORD_CODE_PREFIX) ? code : UNKNOWN_CODE);
}

/** The first message of each field a `validation-failed` problem names. */
export function fieldErrorsOf(problem: ApiProblem): FieldErrors {
  const fields = Object.entries(problem.errors)
    .filter(([, codes]) => codes.length > 0)
    .map(([field, codes]) => [field, messageOf(codes[0])]);
  return Object.fromEntries(fields) as FieldErrors;
}
