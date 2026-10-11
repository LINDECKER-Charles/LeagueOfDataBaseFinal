import { HttpErrorResponse } from '@angular/common/http';

// The refusals a voter can act on, by the API's ProblemDetails `code`.
const BY_CODE: Readonly<Partial<Record<string, string>>> = {
  'email-not-verified': 'community.vote.refused_unverified',
  'account-too-recent': 'community.vote.refused_too_recent',
  'rate-limited': 'community.vote.refused_too_many',
};

/**
 * Key of the message a refused vote shows: one per refusal the voter can act on, null for
 * any other failure, which only rolls the score back.
 */
export function voteRefusalKey(error: unknown): string | null {
  if (!(error instanceof HttpErrorResponse)) {
    return null;
  }
  const body: unknown = error.error;
  if (typeof body !== 'object' || body === null) {
    return null;
  }
  const code = (body as { code?: unknown }).code;
  return typeof code === 'string' ? (BY_CODE[code] ?? null) : null;
}
