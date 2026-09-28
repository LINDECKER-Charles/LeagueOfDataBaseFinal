import type { ApiProblem } from './api-problem';

/**
 * The message over a refused form, a translation key: none for invalid fields, which say it
 * under each of them, the limit for too many attempts, the generic one for anything else.
 */
export function formErrorOf(problem: ApiProblem): string | null {
  switch (problem.code) {
    case 'validation-failed':
      return null;
    case 'rate-limited':
      return 'auth.flash.too_many_attempts';
    default:
      return 'account.errors.generic';
  }
}
