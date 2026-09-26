import { HttpErrorResponse } from '@angular/common/http';

const GOOGLE_FAILED = 'google-failed';

/**
 * The code the login page shows for a refused exchange: the ProblemDetails `code` of the API
 * (`google-email-unverified`, `account-banned`…), the generic Google failure otherwise.
 */
export function googleFailureCode(error: unknown): string {
  const problem: unknown = error instanceof HttpErrorResponse ? error.error : null;
  return typeof problem === 'object' &&
    problem !== null &&
    'code' in problem &&
    typeof problem.code === 'string'
    ? problem.code
    : GOOGLE_FAILED;
}
