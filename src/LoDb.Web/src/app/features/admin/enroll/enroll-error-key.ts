import type { AdminProblem } from '../shared/http/problem-of';
import { problemKey } from '../shared/http/problem-key';

const CODE_FIELD = 'code';
const INVITATION_FIELD = 'invitation';

/** What a refused confirmation of the authenticator says: a key of the `admin` scope. */
export function enrollErrorKey(problem: AdminProblem): string {
  if ((problem.errors[INVITATION_FIELD] ?? []).length > 0) {
    return 'enroll.errors.invitation';
  }
  if ((problem.errors[CODE_FIELD] ?? []).length > 0) {
    return 'enroll.errors.invalid_code';
  }
  if (problem.code === 'mfa-already-enrolled') {
    return 'enroll.errors.already';
  }
  return problem.code === 'account-locked' ? 'login.errors.locked' : problemKey(problem);
}
