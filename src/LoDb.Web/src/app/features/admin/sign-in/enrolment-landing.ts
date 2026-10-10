import { ADMIN_PATHS } from '../shared/admin-paths';

const QUERY_START = '?';

/**
 * Where an administrator without an authenticator lands once the password is right: the
 * page asked for when it is the enrolment, query included (the invitation of the link
 * `admin mfa-invite` prints), the enrolment alone otherwise.
 */
export function enrolmentLanding(landing: string): string {
  const [path] = landing.split(QUERY_START, 1);
  return path === ADMIN_PATHS.enroll ? landing : ADMIN_PATHS.enroll;
}
